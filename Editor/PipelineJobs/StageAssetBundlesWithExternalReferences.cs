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
using static BundleKit.Utility.RemappingUtility;
using CalculateAssetDependencyData = UnityEditor.Build.Pipeline.Tasks.CalculateAssetDependencyData;
using CalculateSceneDependencyData = UnityEditor.Build.Pipeline.Tasks.CalculateSceneDependencyData;
using GenerateBundleCommands = UnityEditor.Build.Pipeline.Tasks.GenerateBundleCommands;
using GenerateBundleMaps = UnityEditor.Build.Pipeline.Tasks.GenerateBundleMaps;
using GenerateBundlePacking = UnityEditor.Build.Pipeline.Tasks.GenerateBundlePacking;
using UpdateBundleObjectLayout = UnityEditor.Build.Pipeline.Tasks.UpdateBundleObjectLayout;

namespace BundleKit.PipelineJobs
{
    [PipelineSupport(typeof(Pipeline)), RequiresManifestDatumType(typeof(AssetBundleDefinitions))]
    public class StageAssetBundlesWithExternalReferences : PipelineJob
    {
        [EnumFlag]
        public ContentBuildFlags contentBuildFlags = ContentBuildFlags.None;
        public BuildTarget buildTarget = BuildTarget.StandaloneWindows;
        public BuildTargetGroup buildTargetGroup = BuildTargetGroup.Standalone;
        public Compression compression = Compression.Uncompressed;
        public bool remapAssetsReferences;
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
                BundleCompression = compression.AsBuildCompression(),
                ContentBuildFlags = contentBuildFlags
            };

            var content = new BundleBuildContent(builds);

            var remapContext = new RemapContext();
            var context = new List<IContextObject>();
            var returnCode = ContentPipeline.BuildAssetBundles(parameters, content, out var result, BuildTaskList(), remapContext, new AssetFileIdentifier(AssetsReferenceBundle), AssetsReferenceBundle);

            if (returnCode < 0)
            {
                throw new Exception($"AssetBundle Build Incomplete: {returnCode}");
            }

            if (remapAssetsReferences)
            {
                RemapBundle(bundleArtifactPath, builds, remapContext, compression);
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
            buildTasks.Add(new CalculateAssetDependencyData());
            buildTasks.Add(new CalculateSceneDependencyData());
            buildTasks.Add(new StripUnusedSpriteSources());
            buildTasks.Add(new PostDependencyCallback());

            // Packing
            buildTasks.Add(new Building.GenerateBundlePacking());
            buildTasks.Add(new UpdateBundleObjectLayout());
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
                                if (filePath.IndexOf(bundleName, StringComparison.OrdinalIgnoreCase) >= 0)
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

        private AssetBundleBuild[] GetAssetBundleBuilds(AssetBundleDefinitions[] assetBundleDefs, List<string> explicitAssetPaths)
        {
            var ignoredExtensions = new[] { ".dll", ".cs" };
            var logBuilder = new StringBuilder();
            var builds = new AssetBundleBuild[assetBundleDefs.Sum(abd => abd.assetBundles.Length) + 1];
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

            logBuilder.AppendLine("--------------------------------------------------");
            logBuilder.AppendLine($"Defining bundle: resources.assets");
            logBuilder.AppendLine();
            var mainAssetPath = AssetDatabase.GetAssetPath(AssetsReferenceBundle);
            builds[builds.Length - 1] = new AssetBundleBuild()
            {
                assetBundleName = "resources.assets",
                assetNames = new[] { mainAssetPath }
            };
            foreach (var asset in builds[builds.Length - 1].assetNames)
                logBuilder.AppendLine(asset);
            logBuilder.AppendLine("--------------------------------------------------");
            logBuilder.AppendLine();

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
    }
}