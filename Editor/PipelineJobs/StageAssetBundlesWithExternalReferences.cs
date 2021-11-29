using BundleKit.Building;
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
using BundleKit.Assets;
using BundleKit.Bundles;
using AssetsTools.NET.Extra;
using AssetsTools.NET;

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
        public bool simulate;

        [PathReferenceResolver]
        public string BundleArtifactPath = "<AssetBundleStaging>";

        public override Task Execute(Pipeline pipeline)
        {
            AssetDatabase.SaveAssets();

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

            var parameters = new BundleBuildParameters(buildTarget, buildTargetGroup, bundleArtifactPath);
            parameters.BundleCompression = ConvertCopressionEnum(buildCompression);
            parameters.ContentBuildFlags = contentBuildFlags;

            var content = new BundleBuildContent(builds);
            var returnCode = ContentPipeline.BuildAssetBundles(parameters, content, out var result, BuildTaskList(), new Unity5PackedIdentifiers());
            if (returnCode < ReturnCode.Success)
            {
                throw new Exception($"Failed to build asset bundles with {returnCode} return code");
            }

            var assetsReplacers = new List<AssetsReplacer>();
            var bundleReplacers = new List<BundleReplacer>();
            var referenceContext = "Removed Assets\r\n";
            foreach (var build in builds)
            {
                //Load AssetBundle using AssetTools.Net
                //Modify AssetBundles by removing assets named (Asset Reference) and all their dependencies.
                var am = new AssetsManager();
                var path = Path.Combine(bundleArtifactPath, build.assetBundleName);
                using (var stream = File.OpenRead(path))
                {
                    var bun = am.LoadBundleFile(stream);
                    var bundleAssetsFile = am.LoadAssetsFileFromBundle(bun, 0);
                    var assetBundleAsset = bundleAssetsFile.table.GetAssetsOfType((int)AssetClassID.AssetBundle)[0];
                    var assetBundleExtAsset = am.GetExtAsset(bundleAssetsFile, 0, assetBundleAsset.index);
                    foreach (var assetFileInfo in bundleAssetsFile.table.assetFileInfo)// m_Container.children
                    {
                        if (!assetFileInfo.ReadName(bundleAssetsFile.file, out var name)) continue;
                        if (!name.Contains("(Assets Reference)")) continue;
                        long pathId = assetFileInfo.index;


                        var type = (AssetClassID)assetFileInfo.curFileType;
                        var assetExt = am.GetExtAsset(bundleAssetsFile, 0, assetFileInfo.index);
                        AssetTypeValueField baseField = assetExt.instance.GetBaseField();

                        //add remover for this asset
                        var remover = new AssetsRemover(0, pathId, (int)type);
                        assetsReplacers.Add(remover);

                        //add remover for all dependencies on this asset
                        foreach (var (asset, pptr, assetName, assetFileName, fileId, pathID, depth) in bundleAssetsFile.GetDependentAssetIds(am, baseField))
                        {
                            remover = new AssetsRemover(fileId, pathID, (int)asset.info.curFileType,
                                                        AssetHelper.GetScriptIndex(asset.file.file, asset.info));
                            assetsReplacers.Add(remover);
                        }

                        referenceContext += $"1. ({type}) \"{name}\" {{FileID: 0, PathID: {pathId} }}\r\n";
                    }

                    byte[] newAssetData;
                    using (var bundleStream = new MemoryStream())
                    using (var writer = new AssetsFileWriter(bundleStream))
                    {
                        bundleAssetsFile.file.Write(writer, 0,
                            assetsReplacers.OrderBy(repl => repl.GetPathID()).ToList(), 0);

                        newAssetData = bundleStream.ToArray();
                    }
                    var bundleReplacer = new BundleReplacerFromMemory(bundleAssetsFile.name, bundleAssetsFile.name, true, newAssetData, -1);
                    bundleReplacers.Add(bundleReplacer);

                    using (var file = File.OpenWrite(path))
                    using (var writer = new AssetsFileWriter(file))
                        bun.file.Write(writer, bundleReplacers);
                }

                //Add a dependency to the asset bundle for resources.assets
                //Update references to (Asset Reference) with reference to asset in resources.assets
            }

            CopyModifiedAssetBundles(bundleArtifactPath, pipeline);
            
            return Task.CompletedTask;
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
            buildTasks.Add(new CalculateSceneDependencyData());
            buildTasks.Add(new CalculateAssetDependencyData());
            buildTasks.Add(new StripUnusedSpriteSources());
            buildTasks.Add(new PostDependencyCallback());

            // Packing
            buildTasks.Add(new Building.GenerateBundlePacking());
            buildTasks.Add(new Building.UpdateBundleObjectLayout());
            buildTasks.Add(new Building.GenerateBundleCommands());
            buildTasks.Add(new GenerateSubAssetPathMaps());
            buildTasks.Add(new GenerateBundleMaps());
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
                            var destFileName = Path.GetFileNameWithoutExtension(filePath);
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

        private static UnityEngine.BuildCompression ConvertCopressionEnum(Compression compression)
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