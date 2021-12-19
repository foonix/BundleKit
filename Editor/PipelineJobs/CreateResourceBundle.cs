using AssetsTools.NET;
using AssetsTools.NET.Extra;
using BundleKit.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ThunderKit.Common.Logging;
using ThunderKit.Core.Data;
using ThunderKit.Core.Pipelines;
using UnityEditor;

namespace BundleKit.PipelineJobs
{
    using static AssetToolPipelineMethods;

    [PipelineSupport(typeof(Pipeline))]
    public class CreateResourceBundle : PipelineJob
    {
        [Serializable]
        public struct Filter
        {
            public string[] nameRegex;
            public AssetClassID assetClass;
        }

        public DefaultAsset bundle;
        public string outputDirectory;
        public Filter[] filters;

        AssetsManager am;
        public override Task Execute(Pipeline pipeline)
        {
            using (var progressBar = new ProgressBar("Constructing AssetBundle"))
            {
                if (!AssetDatabase.IsValidFolder(outputDirectory))
                    return Task.CompletedTask;

                var settings = ThunderKitSetting.GetOrCreateSettings<ThunderKitSettings>();
                var dataDirectoryPath = Path.Combine(settings.GamePath, $"{Path.GetFileNameWithoutExtension(settings.GameExecutable)}_Data");
                var sharedAssetsFiles = Directory.EnumerateFiles(dataDirectoryPath, "sharedassets*.assets").ToArray();
                var sharedAssetsresSFiles = Directory.EnumerateFiles(dataDirectoryPath, "sharedassets*.assets.resS").ToArray();
                var resourcesFilePath = Path.Combine(dataDirectoryPath, "resources.assets");
                var ggmPath = Path.Combine(dataDirectoryPath, "globalgamemanagers.assets");
                var templateBundlePath = AssetDatabase.GetAssetPath(bundle);

                var contexts = new List<string>();
                var assetsReplacers = new List<AssetsReplacer>();
                var newContainerChildren = new List<AssetTypeValueField>();

                am = new AssetsManager();

                //load data for classes
                var classDataPath = Path.Combine("Packages", "com.passivepicasso.bundlekit", "Library", "classdata.tpk");
                am.LoadClassPackage(classDataPath);
                var visited = new HashSet<AssetID>();

                IEnumerable<Assets.AssetData> collected = sharedAssetsFiles
                    .Prepend(resourcesFilePath)
                    .Prepend(ggmPath)
                    .SelectMany(p =>
                        filters.SelectMany(filter =>
                        {
                            var nameRegex = filter.nameRegex.Select(reg => new Regex(reg)).ToArray();
                            return CollectAssets(am, am.GetAssetsInst(p), visited, nameRegex, filter.assetClass, progressBar);
                        })
                    );

                var assetsByFile = collected.GroupBy(data => data.AssetFileName).ToArray();
                foreach (var assetGroup in assetsByFile)
                {
                    newContainerChildren.Clear();
                    assetsReplacers.Clear();
                    var groupArray = assetGroup.ToArray();
                    var dupeGroups = groupArray.GroupBy(data => data.PathId).ToArray();
                    var assets = dupeGroups.Select(group => group.First()).ToList();
                    string fileName = assetGroup.Key;
                    //if (fileName == "unity_builtin_extra") continue;
                    var assetsFileInst = assets.First().AssetExt.file;
                    var outputPath = Path.Combine(outputDirectory, $"{fileName}reference");

                    am.PrepareNewBundle(templateBundlePath, out var bun, out var bundleAssetsFile, out var assetBundleExtAsset);
                    // Update bundle assets name and bundle name to the name specified in outputAssetBundlePath
                    var bundleBaseField = assetBundleExtAsset.instance.GetBaseField();
                    bundleBaseField.SetValue("m_Name", fileName);
                    bundleBaseField.SetValue("m_AssetBundleName", fileName);

                    bundleAssetsFile.file.dependencies.dependencies.Clear();
                    foreach (var dependency in assetsFileInst.file.dependencies.dependencies)
                        bundleAssetsFile.AddDependency(dependency.assetPath);

                    UpdateAssetBundleDependencies(bundleBaseField, bundleAssetsFile.file.dependencies.dependencies);

                    // Get container for populating asset listings
                    var containerArray = bundleBaseField.GetField("m_Container/Array");
                    foreach (var (asset, assetName, assetFileName, fileId, pathId, depth) in assets)
                    {
                        var assetBaseField = asset.instance.GetBaseField();
                        var fieldType = assetBaseField.GetFieldType();
                        var streamData = assetBaseField.Get("m_StreamData");
                        if (streamData?.children != null)
                        {
                            var path = streamData.Get("path");
                            var streamPath = path.GetValue().AsString();
                            if (!string.IsNullOrEmpty(streamPath))
                                assetBaseField.SetValue("m_StreamData/path", Path.Combine(dataDirectoryPath, streamPath).Replace("\\", "/"));
                        }
                        //if (assetBaseField.GetFieldType() == "Texture2D")
                        //{
                        //    var texName = assetBaseField.GetValue("m_Name").AsString();
                        //    var imageDataValue = assetBaseField.GetValue("image data");
                        //    var imageData = imageDataValue.AsByteArray();
                        //}

                        var otherBytes = asset.instance.WriteToByteArray();
                        var currentAssetReplacer = new AssetsReplacerFromMemory(0, pathId, (int)asset.info.curFileType,
                                                                                AssetHelper.GetScriptIndex(asset.file.file, asset.info),
                                                                                otherBytes);
                        assetsReplacers.Add(currentAssetReplacer);

                        // Create entry in m_Container to make this asset visible in the API, otherwise said the asset can be found with AssetBundles.LoadAsset* methods
                        newContainerChildren.Add(containerArray.CreateEntry(assetName, 0, pathId));
                    }

                    containerArray.SetChildrenList(newContainerChildren.ToArray());

                    bundleBaseField.GetField("m_PreloadTable/Array")
                        .SetChildrenList(Array.Empty<AssetTypeValueField>());

                    //Save changes for building new bundle file
                    var newAssetBundleBytes = bundleBaseField.WriteToByteArray();
                    assetsReplacers.Add(new AssetsReplacerFromMemory(0, assetBundleExtAsset.info.index, (int)assetBundleExtAsset.info.curFileType, 0xFFFF, newAssetBundleBytes));
                    assetsReplacers = assetsReplacers.GroupBy(repl => repl.GetPathID()).Select(g => g.First()).OrderBy(repl => repl.GetPathID()).ToList();

                    if (File.Exists(outputPath)) File.Delete(outputPath);

                    byte[] newAssetData;
                    using (var bundleStream = new MemoryStream())
                    using (var writer = new AssetsFileWriter(bundleStream))
                    {
                        //We need to order the replacers by their pathId for Unity to be able to read the Bundle correctly.
                        bundleAssetsFile.file.Write(writer, 0, assetsReplacers, 0, am.classFile);
                        newAssetData = bundleStream.ToArray();
                    }

                    using (var file = File.OpenWrite(outputPath))
                    using (var writer = new AssetsFileWriter(file))
                    {
                        var assetsFileName = $"CAB-{GUID.Generate()}";
                        bun.file.Write(writer, new List<BundleReplacer>
                        {
                            new BundleReplacerFromMemory(bundleAssetsFile.name, fileName, true, newAssetData, newAssetData.Length),
                        });
                    }
                }

                pipeline.Log(LogLevel.Information, $"Finished Building Bundle", contexts.ToArray());
                am.UnloadAll(true);

                foreach (var resSPath in sharedAssetsresSFiles)
                {
                    if (File.Exists(Path.GetFileName(resSPath))) continue;
                    File.Copy(resSPath, Path.GetFileName(resSPath));
                }
            }
            AssetDatabase.Refresh();
            return Task.CompletedTask;
        }

    }
}