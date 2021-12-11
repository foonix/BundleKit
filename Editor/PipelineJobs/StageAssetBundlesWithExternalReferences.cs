using AssetsTools.NET;
using AssetsTools.NET.Extra;
using BundleKit.Building;
using BundleKit.Building.Contexts;
using BundleKit.Bundles;
using BundleKit.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThunderKit.Core.Attributes;
using ThunderKit.Core.Data;
using ThunderKit.Core.Manifests.Datums;
using ThunderKit.Core.Paths;
using ThunderKit.Core.Pipelines;
using UnityEditor;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Tasks;
using UnityEditor.Compilation;
using UnityEngine;

namespace BundleKit.PipelineJobs
{
    [PipelineSupport(typeof(Pipeline)), RequiresManifestDatumType(typeof(AssetBundleDefinitions))]
    public class StageAssetBundlesWithExternalReferences : PipelineJob
    {
        public enum Compression { Uncompressed, LZMA, LZ4 }

        [EnumFlag]
        public ContentBuildFlags contentBuildFlags = ContentBuildFlags.None;
        public BuildTarget buildTarget = BuildTarget.StandaloneWindows;
        public BuildTargetGroup buildTargetGroup = BuildTargetGroup.Standalone;
        public Compression buildCompression = Compression.Uncompressed;
        public AssetsReferenceBundle AssetsReferenceBundle;
        public bool simulate;

        [PathReferenceResolver]
        public string BundleArtifactPath = "<AssetBundleStaging>";

        public override Task Execute(Pipeline pipeline)
        {
            //AssetDatabase.SaveAssets();

            var assetBundleDefs = pipeline.Datums.OfType<AssetBundleDefinitions>().ToArray();
            if (assetBundleDefs.Length == 0)
            {
                return Task.CompletedTask;
            }

            var bundleArtifactPath = BundleArtifactPath.Resolve(pipeline, this);
            Directory.CreateDirectory(bundleArtifactPath);

            var explicitAssets = assetBundleDefs
                                .SelectMany(abd => abd.assetBundles)
                                .SelectMany(ab => ab.assets)
                                .Where(a => a)
                                .ToArray();

            var explicitAssetPaths = new List<string>();
            PopulateWithExplicitAssets(explicitAssets, explicitAssetPaths);

            var builds = GetAssetBundleBuilds(assetBundleDefs, explicitAssetPaths);

            if (simulate)
            {
                return Task.CompletedTask;
            }

            var parameters = new BundleBuildParameters(buildTarget, buildTargetGroup, bundleArtifactPath)
            {
                BundleCompression = ConvertCompression(buildCompression),
                ContentBuildFlags = contentBuildFlags
            };

            var content = new BundleBuildContent(builds);
            var remapContext = new RemapContext();
            var returnCode = ContentPipeline.BuildAssetBundles(parameters, content, out var result, BuildTaskList(), new AssetFileIdentifier(AssetsReferenceBundle), remapContext);

            if (returnCode < 0)
            {
                throw new Exception($"AssetBundle Build Incomplete: {returnCode}");
            }

            var assetsReplacers = new List<AssetsReplacer>();
            var bundleReplacers = new List<BundleReplacer>();
            foreach (var build in builds)
            {
                assetsReplacers.Clear();
                bundleReplacers.Clear();
                //Load AssetBundle using AssetTools.Net
                //Modify AssetBundles by removing assets named (Asset Reference) and all their dependencies.
                var am = new AssetsManager();
                var path = Path.Combine(bundleArtifactPath, build.assetBundleName);
                var temppath = Path.Combine(bundleArtifactPath, $"{build.assetBundleName}.tmp");

                if (File.Exists(temppath)) File.Delete(temppath);
                File.Move(path, temppath);

                using (var stream = File.OpenRead(temppath))
                {
                    var bun = am.LoadBundleFile(stream);
                    var fileCount = bun.file.NumFiles;
                    var assetsFile = am.LoadAssetsFileFromBundle(bun, 0);
                    var dependencies = assetsFile.file.dependencies.dependencies;
                    var initialDependencyCount = dependencies.Count;

                    //Update preload table before updating anything else
                    for (int i = 0; i < fileCount; i++)
                    {
                        assetsFile = am.LoadAssetsFileFromBundle(bun, i);
                        if (assetsFile == null) continue; // This will occur if the index of this file is for a resS file which we don't need to process here.

                        var bundleAssets = assetsFile.table.GetAssetsOfType((int)AssetClassID.AssetBundle);
                        if (bundleAssets.Count > 0)
                        {
                            var assetBundleAsset = bundleAssets[0];
                            UpdatePreloadTable(am, assetsFile, assetBundleAsset, remapContext);
                        }
                    }
                    for (int i = 0; i < fileCount; i++)
                    {
                        assetsFile = am.LoadAssetsFileFromBundle(bun, i);
                        if (assetsFile == null) continue; // This will occur if the index of this file is for a resS file which we don't need to process here.

                        var bundleAssets = assetsFile.table.GetAssetsOfType((int)AssetClassID.AssetBundle);
                        if (bundleAssets.Count > 0)
                        {
                            var assetBundleAsset = bundleAssets[0];
                            var bundleReplacer = RemapExternalReferences(assetsReplacers, am, assetsFile, assetBundleAsset, remapContext);
                            bundleReplacers.Add(bundleReplacer);
                        }
                    }

                    using (var file = File.OpenWrite(path))
                    using (var writer = new AssetsFileWriter(file))
                        bun.file.Write(writer, bundleReplacers);

                    if (buildCompression != Compression.Uncompressed)
                        using (var file = File.OpenWrite(path))
                        using (var writer = new AssetsFileWriter(file))
                            switch (buildCompression)
                            {
                                case Compression.LZMA:
                                    bun.file.Pack(bun.file.reader, writer, AssetBundleCompressionType.LZMA);
                                    break;
                                case Compression.LZ4:
                                    bun.file.Pack(bun.file.reader, writer, AssetBundleCompressionType.LZ4);
                                    break;
                            }
                }

                if (File.Exists(temppath)) File.Delete(temppath);
            }

            CopyModifiedAssetBundles(bundleArtifactPath, pipeline);

            return Task.CompletedTask;
        }

        private static BundleReplacerFromMemory RemapExternalReferences(List<AssetsReplacer> assetsReplacers, AssetsManager am, AssetsFileInstance assetsFileInst, AssetFileInfoEx assetBundleInfo, RemapContext remapContext)
        {
            var assetBundleExtAsset = am.GetExtAsset(assetsFileInst, 0, assetBundleInfo.index);
            var bundleBaseField = assetBundleExtAsset.instance.GetBaseField();
            var dependencies = assetsFileInst.file.dependencies.dependencies;

            var dependencyArray = bundleBaseField.Get("m_Dependencies", "Array");

            var dependencyFieldChildren = new List<AssetTypeValueField>();

            var settings = ThunderKitSetting.GetOrCreateSettings<ThunderKitSettings>();

            var remap = remapContext.AssetMaps
                .Where(map => map.name.Equals(assetsFileInst.name))
                .ToDictionary(map => (map.BundlePointer.fileId, map.BundlePointer.pathId),
                              map => (fileId: dependencies.Count + 1, map.ResourcePointer.pathId));


            foreach (var assetFileInfo in assetsFileInst.table.assetFileInfo)
            {
                if (!assetFileInfo.ReadName(assetsFileInst.file, out var name))
                    continue;

                if (name.Contains("(Asset Reference)"))
                {
                    long pathId = assetFileInfo.index;
                    var type = (AssetClassID)assetFileInfo.curFileType;
                    var assetExt = am.GetExtAsset(assetsFileInst, 0, assetFileInfo.index);
                    AssetTypeValueField baseField = assetExt.instance.GetBaseField();

                    //add remover for this asset
                    if (!assetsReplacers.Any(ar => ar.GetPathID() == pathId))
                        assetsReplacers.Add(new AssetsRemover(0, pathId, (int)type));

                    //add remover for all dependencies on this asset
                    foreach (var (asset, pptr, assetName, assetFileName, fileId, pathID, depth) in assetExt.file.GetDependentAssetIds(baseField, am))
                    {
                        if (!assetsReplacers.Any(ar => ar.GetPathID() == pathID && ar.GetFileID() == fileId))
                            assetsReplacers.Add(new AssetsRemover(fileId, pathID, (int)asset.info.curFileType,
                                                    AssetHelper.GetScriptIndex(asset.file.file, asset.info)));
                    }
                }
                else if (name.Contains("(Custom Asset)"))
                {

                    var assetExt = am.GetExtAsset(assetsFileInst, 0, assetFileInfo.index);
                    AssetTypeValueField baseField = assetExt.instance.GetBaseField();

                    //add remover for all dependencies on this asset
                    foreach (var (asset, pptr, assetName, assetFileName, fileId, pathID, depth) in assetExt.file.GetDependentAssetIds(baseField, am))
                    {
                        if (!assetsReplacers.Any(ar => ar.GetPathID() == pathID && ar.GetFileID() == fileId))
                            assetsReplacers.Add(new AssetsRemover(fileId, pathID, (int)asset.info.curFileType,
                                                    AssetHelper.GetScriptIndex(asset.file.file, asset.info)));
                    }
                }
            }

            foreach (var assetFileInfo in assetsFileInst.table.assetFileInfo)
            {
                if (assetFileInfo == assetBundleInfo) continue;
                if (assetsReplacers.Any(ar => ar.GetPathID() == assetFileInfo.index))
                    continue;

                var asset = am.GetExtAsset(assetsFileInst, 0, assetFileInfo.index);
                AssetTypeValueField assetBaseField = asset.instance.GetBaseField();
                assetBaseField.RemapPPtrs(remap);
                var otherBytes = asset.instance.WriteToByteArray();
                var currentAssetReplacer = new AssetsReplacerFromMemory(0, assetFileInfo.index, (int)asset.info.curFileType,
                                                                        AssetHelper.GetScriptIndex(asset.file.file, asset.info),
                                                                        otherBytes);
                assetsReplacers.Add(currentAssetReplacer);
            }

            //Copy dependencies array from resources.assets and add dependency to resources.assets
            string fullResourcesPath = $"{settings.GamePath}\\{Path.GetFileNameWithoutExtension(settings.GameExecutable)}_Data\\resources.assets";
            dependencies.Add(
                new AssetsFileDependency
                {
                    assetPath = fullResourcesPath,
                    originalAssetPath = fullResourcesPath,
                    bufferedPath = string.Empty
                }
            );
            assetsFileInst.file.dependencies.dependencyCount = dependencies.Count;
            assetsFileInst.file.dependencies.dependencies = dependencies;
            foreach (var dep in dependencies)
            {
                var depTemplate = ValueBuilder.DefaultValueFieldFromArrayTemplate(dependencyArray);
                depTemplate.GetValue().Set(dep.assetPath);
                dependencyFieldChildren.Add(depTemplate);
            }
            dependencyArray.SetChildrenList(dependencyFieldChildren.ToArray());

            var newAssetBundleBytes = bundleBaseField.WriteToByteArray();
            assetsReplacers.Add(new AssetsReplacerFromMemory(0, assetBundleInfo.index, (int)assetBundleInfo.curFileType, 0xFFFF, newAssetBundleBytes));

            assetsReplacers = assetsReplacers.OrderBy(repl => repl.GetPathID()).ToList();
            byte[] newAssetData;
            using (var bundleStream = new MemoryStream())
            using (var writer = new AssetsFileWriter(bundleStream))
            {
                assetsFileInst.file.Write(writer, 0, assetsReplacers);
                newAssetData = bundleStream.ToArray();
            }
            var bundleReplacer = new BundleReplacerFromMemory(assetsFileInst.name, assetsFileInst.name, true, newAssetData, -1);
            return bundleReplacer;
        }

        private static void UpdatePreloadTable(AssetsManager am, AssetsFileInstance assetsFileInst, AssetFileInfoEx assetBundleInfo, RemapContext remapContext)
        {
            var assetBundleExtAsset = am.GetExtAsset(assetsFileInst, 0, assetBundleInfo.index);
            var bundleBaseField = assetBundleExtAsset.instance.GetBaseField();
            var preloadTableArray = bundleBaseField.GetField("m_PreloadTable/Array");
            var preloadFieldChildren = new List<AssetTypeValueField>();

            var count = assetsFileInst.file.dependencies.dependencyCount + 1;

            var remap = remapContext.AssetMaps
                .Where(map => map.name.Equals(assetsFileInst.name))
                .ToDictionary(map => (map.BundlePointer.fileId, map.BundlePointer.pathId),
                              map => (fileId: count + 1, map.ResourcePointer.pathId));

            var containerArray = bundleBaseField.GetField("m_Container/Array");
            // Setup preload table
            foreach (var assetIndexField in containerArray.GetChildrenList())
            {
                var name = assetIndexField.GetValue("first").AsString();
                long pathId = assetIndexField.GetValue("second/asset/m_PathID").AsInt64();
                if (pathId == 0) continue;
                var assetExt = am.GetExtAsset(assetsFileInst, 0, pathId);
                var baseField = assetExt.instance.GetBaseField();

                foreach (var dependency in assetExt.file.GetDependentAssetIds(baseField, am))
                {
                    var preloadEntry = ValueBuilder.DefaultValueFieldFromArrayTemplate(preloadTableArray);
                    var key = (fileId: dependency.FileId, pathId: dependency.PathId);
                    if (remap.ContainsKey(key))
                    {
                        preloadEntry.SetValue("m_FileID", remap[key].fileId);
                        preloadEntry.SetValue("m_PathID", remap[key].pathId);
                    }
                    else
                    {
                        preloadEntry.SetValue("m_FileID", key.fileId);
                        preloadEntry.SetValue("m_PathID", key.pathId);
                    }
                    preloadFieldChildren.Add(preloadEntry);
                }
            }
            if (preloadFieldChildren.Any())
                preloadTableArray.SetChildrenList(preloadFieldChildren.ToArray());
        }

        private List<IBuildTask> BuildTaskList()
        {
            var buildTasks = new List<IBuildTask>();

            // Setup
            buildTasks.Add(new SwitchToBuildPlatform());
            buildTasks.Add(new RebuildSpriteAtlasCache());

            //buildTasks.Add(new BuildPlayerScripts());
            buildTasks.Add(new PostScriptsCallback());

            // Dependency
            buildTasks.Add(new UnityEditor.Build.Pipeline.Tasks.CalculateSceneDependencyData());
            buildTasks.Add(new CalculateAssetDependencyData());
            buildTasks.Add(new StripUnusedSpriteSources());
            buildTasks.Add(new PostDependencyCallback());

            // Packing
            buildTasks.Add(new Building.GenerateBundlePacking());
            buildTasks.Add(new Building.UpdateBundleObjectLayout());
            buildTasks.Add(new Building.GenerateBundleCommands());
            buildTasks.Add(new GenerateSubAssetPathMaps());
            buildTasks.Add(new Building.GenerateBundleMaps(AssetsReferenceBundle));
            buildTasks.Add(new PostPackingCallback());

            //// Writing
            buildTasks.Add(new WriteSerializedFiles());
            buildTasks.Add(new ArchiveAndCompressBundles());
            buildTasks.Add(new PostWritingCallback());

            return buildTasks;
        }


        private void CopyModifiedAssetBundles(string bundleArtifactPath, Pipeline pipeline)
        {
            for (pipeline.ManifestIndex = 0; pipeline.ManifestIndex < pipeline.Manifests.Length; pipeline.ManifestIndex++)
            {
                var manifest = pipeline.Manifest;
                foreach (var assetBundleDef in manifest.Data.OfType<AssetBundleDefinitions>())
                {
                    var bundleNames = assetBundleDef.assetBundles.Select(ab => ab.assetBundleName).ToArray();
                    foreach (var outputPath in assetBundleDef.StagingPaths.Select(path => path.Resolve(pipeline, this)))
                    {
                        foreach (string dirPath in Directory.GetDirectories(bundleArtifactPath, "*", SearchOption.AllDirectories))
                            Directory.CreateDirectory(dirPath.Replace(bundleArtifactPath, outputPath));

                        foreach (string filePath in Directory.GetFiles(bundleArtifactPath, "*", SearchOption.AllDirectories))
                        {
                            bool found = false;
                            foreach (var bundleName in bundleNames)
                            {
                                if (filePath.IndexOf(bundleName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    found = true;
                                    break;
                                }
                            }
                            if (!found) continue;
                            var destFolder = Path.GetDirectoryName(filePath.Replace(bundleArtifactPath, outputPath));
                            var destFileName = Path.GetFileName(filePath);
                            Directory.CreateDirectory(destFolder);
                            FileUtil.ReplaceFile(filePath, Path.Combine(destFolder, destFileName));
                        }

                        //.manifest doesn't exist when building with ContentPipeline.BuildAssetBundles()
                        //but do we really need it?
                        //
                        //var manifestSource = Path.Combine(bundleArtifactPath, $"{Path.GetFileName(bundleArtifactPath)}.manifest");
                        //var manifestDestination = Path.Combine(outputPath, $"{manifest.Identity.Name}.manifest");
                        //FileUtil.ReplaceFile(manifestSource, manifestDestination);
                    }
                }
            }
            pipeline.ManifestIndex = -1;
        }

        private static AssetBundleBuild[] GetAssetBundleBuilds(AssetBundleDefinitions[] assetBundleDefs, List<string> explicitAssetPaths)
        {
            var ignoredExtensions = new[] { ".dll", ".cs" };
            var logBuilder = new StringBuilder();
            var builds = new AssetBundleBuild[assetBundleDefs.Sum(abd => abd.assetBundles.Length)];
            logBuilder.AppendLine($"Defining {builds.Length} AssetBundleBuilds");

            var buildsIndex = 0;
            for (int defIndex = 0; defIndex < assetBundleDefs.Length; defIndex++)
            {
                var assetBundleDef = assetBundleDefs[defIndex];
                var playerAssemblies = CompilationPipeline.GetAssemblies();
                var assemblyFiles = playerAssemblies.Select(pa => pa.outputPath).ToArray();
                var sourceFiles = playerAssemblies.SelectMany(pa => pa.sourceFiles).ToArray();

                for (int i = 0; i < assetBundleDef.assetBundles.Length; i++)
                {
                    var def = assetBundleDef.assetBundles[i];


                    var build = builds[buildsIndex];

                    var assets = new List<string>();

                    logBuilder.AppendLine("--------------------------------------------------");
                    logBuilder.AppendLine($"Defining bundle: {def.assetBundleName}");
                    logBuilder.AppendLine();

                    var firstAsset = def.assets.FirstOrDefault(x => x is SceneAsset);

                    if (firstAsset != null) assets.Add(AssetDatabase.GetAssetPath(firstAsset));
                    else
                    {
                        PopulateWithExplicitAssets(def.assets, assets);

                        var dependencies = assets
                            .SelectMany(assetPath => AssetDatabase.GetDependencies(assetPath))
                            .Where(assetPath => !ignoredExtensions.Contains(Path.GetExtension(assetPath)))
                            .Where(dap => !explicitAssetPaths.Contains(dap))
                            .Where(dap => AssetDatabase.GetMainAssetTypeAtPath(dap) != typeof(AssetsReferenceBundle))
                            .ToArray();
                        assets.AddRange(dependencies);
                    }

                    build.assetNames = assets
                        .Select(ap => ap.Replace("\\", "/"))
                        .Where(dap => !ArrayUtility.Contains(Constants.ExcludedExtensions, Path.GetExtension(dap)) &&
                                      !ArrayUtility.Contains(sourceFiles, dap) &&
                                      !ArrayUtility.Contains(assemblyFiles, dap) &&
                                      !AssetDatabase.IsValidFolder(dap))
                        .Distinct()
                        .ToArray();
                    build.assetBundleName = def.assetBundleName;
                    builds[buildsIndex] = build;
                    buildsIndex++;

                    foreach (var asset in build.assetNames)
                        logBuilder.AppendLine(asset);

                    logBuilder.AppendLine("--------------------------------------------------");
                    logBuilder.AppendLine();
                }
            }

            Debug.Log(logBuilder.ToString());

            return builds;
        }

        private static void PopulateWithExplicitAssets(IEnumerable<UnityEngine.Object> inputAssets, List<string> outputAssets)
        {
            foreach (var asset in inputAssets)
            {
                if (!asset)
                {
                    continue;
                }
                var assetPath = AssetDatabase.GetAssetPath(asset);

                if (AssetDatabase.IsValidFolder(assetPath))
                {
                    var files = Directory.GetFiles(assetPath, "*", SearchOption.AllDirectories);
                    var assets = files.Select(path => AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path));
                    PopulateWithExplicitAssets(assets, outputAssets);
                }
                else if (asset is UnityPackage up)
                {
                    PopulateWithExplicitAssets(up.AssetFiles, outputAssets);
                }
                else
                {
                    outputAssets.Add(assetPath);
                }
            }
        }

        private static UnityEngine.BuildCompression ConvertCompression(Compression compression)
        {
            switch (compression)
            {
                case Compression.Uncompressed:
                    return UnityEngine.BuildCompression.Uncompressed;
                case Compression.LZMA:
                    return UnityEngine.BuildCompression.LZMA;
                case Compression.LZ4:
                    return UnityEngine.BuildCompression.LZ4;
            }

            throw new NotSupportedException();
        }
    }
}