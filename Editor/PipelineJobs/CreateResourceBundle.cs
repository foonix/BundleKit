using AssetsTools.NET;
using AssetsTools.NET.Extra;
using BundleKit.Assets;
using BundleKit.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ThunderKit.Common.Logging;
using ThunderKit.Core.Pipelines;
using UnityEditor;

namespace BundleKit.PipelineJobs
{
    using static AssetToolPipelineMethods;

    [PipelineSupport(typeof(Pipeline))]
    public class CreateResourceBundle : PipelineJob
    {
        public DefaultAsset bundle;
        public string outputAssetBundlePath;
        public string[] nameRegexFilters;
        public AssetClassID[] classes;

        AssetsManager am;
        public override Task Execute(Pipeline pipeline)
        {
            using (var progressBar = new ProgressBar("Constructing AssetBundle"))
            {
                InitializeContainers(nameRegexFilters, out var assetsReplacers, out var contexts, out var newContainerChildren, out var nameRegex);
                InitializePaths(bundle, outputAssetBundlePath, out var resourcesFilePath, out var ggmPath, out var fileName, out var path, out var dataDirectoryPath);
                InitializeAssetTools(resourcesFilePath, path, ref am, out var bun, out var bundleAssetsFile, out var resourcesInst, out var assetBundleAsset, out var assetBundleExtAsset, out var bundleBaseField);

                var assets = CollectAssets(am, resourcesInst, nameRegex, classes, progressBar)
                                    .Where(asset => asset.AssetFileName.Equals("resources.assets"))
                                    .Distinct()
                                    .OrderBy(ta => ta.PathId)
                                    .ToArray();

                // Update bundle assets name and bundle name to the name specified in outputAssetBundlePath
                bundleBaseField.SetValue("m_Name", fileName);
                bundleBaseField.SetValue("m_AssetBundleName", fileName);

                bundleAssetsFile.file.dependencies.dependencies.Clear();
                foreach (var dependency in resourcesInst.file.dependencies.dependencies)
                    bundleAssetsFile.AddDependency(dependency.assetPath);

                UpdateAssetBundleDependencies(bundleBaseField, bundleAssetsFile.file.dependencies.dependencies);

                // Get container for populating asset listings
                var containerArray = bundleBaseField.GetField("m_Container/Array");
                foreach (var (asset, assetName, assetFileName, fileId, pathId, depth) in assets)
                {
                    var assetBaseField = asset.instance.GetBaseField();
                    if (assetBaseField.GetFieldType() == "Texture2D")
                    {
                        assetBaseField.SetValue("m_StreamData/path", Path.Combine(dataDirectoryPath, assetBaseField.GetValue("m_StreamData/path").AsString()));
                    }

                    var otherBytes = asset.instance.WriteToByteArray();
                    var currentAssetReplacer = new AssetsReplacerFromMemory(0, pathId, (int)asset.info.curFileType,
                                                                            AssetHelper.GetScriptIndex(asset.file.file, asset.info),
                                                                            otherBytes);
                    assetsReplacers.Add(currentAssetReplacer);

                    // Create entry in m_Container to make this asset visible in the API, otherwise said the asset can be found with AssetBundles.LoadAsset* methods
                    newContainerChildren.Add(containerArray.CreateEntry(assetName, 0, pathId));
                }

                containerArray.SetChildrenList(newContainerChildren.ToArray());
                bundleBaseField.GetField("m_PreloadTable/Array").SetChildrenList(Array.Empty<AssetTypeValueField>());

                //Save changes for building new bundle file
                var newAssetBundleBytes = bundleBaseField.WriteToByteArray();
                assetsReplacers.Add(new AssetsReplacerFromMemory(0, assetBundleAsset.index, (int)assetBundleAsset.curFileType, 0xFFFF, newAssetBundleBytes));
                assetsReplacers = assetsReplacers.OrderBy(repl => repl.GetPathID()).ToList();

                if (File.Exists(outputAssetBundlePath)) File.Delete(outputAssetBundlePath);

                byte[] newAssetData;
                using (var bundleStream = new MemoryStream())
                using (var writer = new AssetsFileWriter(bundleStream))
                {
                    //We need to order the replacers by their pathId for Unity to be able to read the Bundle correctly.
                    bundleAssetsFile.file.Write(writer, 0, assetsReplacers, 0, am.classFile);
                    newAssetData = bundleStream.ToArray();
                }

                using (var file = File.OpenWrite(outputAssetBundlePath))
                using (var writer = new AssetsFileWriter(file))
                {
                    var assetsFileName = $"CAB-{GUID.Generate()}";
                    bun.file.Write(writer, new List<BundleReplacer>
                    {
                        new BundleReplacerFromMemory(bundleAssetsFile.name, "resources.assets", true, newAssetData, newAssetData.Length),
                    });
                }

                pipeline.Log(LogLevel.Information, $"Finished Building Bundle", contexts.ToArray());
                am.UnloadAll(true);
            }
            return Task.CompletedTask;
        }

    }
}