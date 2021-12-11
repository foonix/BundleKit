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
using ThunderKit.Core.Data;
using ThunderKit.Core.Paths;
using ThunderKit.Core.Pipelines;
using UnityEditor;

namespace BundleKit.PipelineJobs
{
    [PipelineSupport(typeof(Pipeline))]
    public class CreateResourceBundle : PipelineJob
    {
        public DefaultAsset bundle;
        public string outputAssetBundlePath;
        public string nameRegexFilter = string.Empty;
        public AssetClassID[] classes;

        private AssetsManager am;
        public override Task Execute(Pipeline pipeline)
        {
            using (var progressBar = new ProgressBar("Constructing AssetBundle"))
            {
                InitializeContainers(out var assetsReplacers, out var contexts, out var targetAssets, out var newContainerChildren, out var nameRegex);
                InitializePaths(out var resourcesFilePath, out var ggmPath, out var fileName, out var path, out var dataDirectoryPath);
                InitializeAssetTools(resourcesFilePath, path, out var bun, out var bundleAssetsFile, out var resourcesInst, out var assetBundleAsset, out var assetBundleExtAsset, out var bundleBaseField);

                targetAssets = CollectAssets(progressBar, targetAssets, nameRegex, resourcesInst);

                var realizedAssetTargets = targetAssets.OrderBy(ta => ta.Depth).Distinct().ToArray();
                long nextId = 2;
                var assetMap = new List<AssetMap>();
                for (int i = 0; i < realizedAssetTargets.Length; i++)
                {
                    var (asset, pptr, assetName, assetFileName, fileId, pathId, depth) = realizedAssetTargets[i];
                    nextId++;
                    //Store map of new fileId and PathId to original fileId and pathid
                    assetMap.Add(new AssetMap
                    {
                        name = assetName,
                        ResourcePointer = (fileId, pathId),
                        BundlePointer = (0, nextId)
                    });
                    realizedAssetTargets[i] = (asset, pptr, assetName, assetFileName, 0, nextId, depth);
                }

                // Update bundle assets name and bundle name to the name specified in outputAssetBundlePath
                bundleBaseField.Get("m_Name").GetValue().Set(fileName);
                bundleBaseField.Get("m_AssetBundleName").GetValue().Set(fileName);

                // Get container for populating asset listings
                var containerArray = bundleBaseField.Get("m_Container").Get("Array");
                var mappingData = assetMap.ToDictionary(map => ((int, long))map.ResourcePointer, map => ((int, long))map.BundlePointer);
                foreach (var (asset, pptr, assetName, assetFileName, fileId, pathId, depth) in realizedAssetTargets)
                {
                    var assetBaseField = asset.instance.GetBaseField();
                    assetBaseField.RemapPPtrs(mappingData);
                    if (assetBaseField.GetFieldType() == "Texture2D")
                    {
                        var streamDataField = assetBaseField.Get("m_StreamData");
                        streamDataField.SetValue("path", Path.Combine(dataDirectoryPath, streamDataField.GetValue("path").AsString()));
                    }

                    var otherBytes = asset.instance.WriteToByteArray();
                    var currentAssetReplacer = new AssetsReplacerFromMemory(0, pathId, (int)asset.info.curFileType,
                                                                            AssetHelper.GetScriptIndex(asset.file.file, asset.info),
                                                                            otherBytes);
                    assetsReplacers.Add(currentAssetReplacer);

                    // Create entry in m_Container to make this asset visible in the API, otherwise said the asset can be found with AssetBundles.LoadAsset* methods
                    newContainerChildren.Add(containerArray.CreateEntry(assetName, 0, pathId));
                }

                //// Create a text asset to store information that will be used to modified bundles built by users to redirect references to the original source files
                var mappingDataFieldTemplate = new AssetTypeTemplateField();
                var cldbType = AssetHelper.FindAssetClassByID(am.classFile, (int)AssetClassID.TextAsset);
                mappingDataFieldTemplate.FromClassDatabase(am.classFile, cldbType, 0);
                var textAssetBaseField = ValueBuilder.DefaultValueFieldFromTemplate(mappingDataFieldTemplate);
                textAssetBaseField.Get("m_Name").GetValue().Set("mappingdata.json");
                var mapArray = new MappingData { AssetMaps = assetMap.ToArray() };
                var mapJson = EditorJsonUtility.ToJson(mapArray, true);
                textAssetBaseField.Get("m_Script").GetValue().Set(mapJson);

                assetsReplacers.Add(new AssetsReplacerFromMemory(0, ++nextId, cldbType.classId, 0xffff, textAssetBaseField.WriteToByteArray()));
                {
                    // Use m_Container to construct an blank element for it
                    var pair = containerArray.CreateEntry(textAssetBaseField.GetValue("m_Name").AsString(), 0, nextId);
                    newContainerChildren.Add(pair);
                }

                containerArray.SetChildrenList(newContainerChildren.ToArray());
                bundleBaseField.Get("m_PreloadTable").Get("Array").SetChildrenList(Array.Empty<AssetTypeValueField>());

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
                        new BundleReplacerFromMemory(bundleAssetsFile.name, assetsFileName, true, newAssetData, newAssetData.Length),
                    });
                }

                pipeline.Log(LogLevel.Information, $"Finished Building Bundle", contexts.ToArray());
            }
            return Task.CompletedTask;
        }

        private IEnumerable<AssetData> CollectAssets(ProgressBar progressBar, IEnumerable<AssetData> targetAssets, Regex nameRegex, AssetsFileInstance resourcesInst)
        {

            // Iterate over all requested Class types and collect the data required to copy over the required asset information
            // This step will recurse over dependencies so all required assets will become available from the resulting bundle
            progressBar.Update(title: "Mapping PathIds to Resource Paths");
            progressBar.Update(title: "Collecting Assets");
            foreach (var assetClass in classes)
            {
                var fileInfos = resourcesInst.table.GetAssetsOfType((int)assetClass);
                for (int x = 0; x < fileInfos.Count; x++)
                {
                    progressBar.Update(title: $"Collecting Assets ({x} / {fileInfos.Count})");

                    var assetFileInfo = fileInfos[x];

                    // If an asset has no name continue, but why?
                    if (!assetFileInfo.ReadName(resourcesInst.file, out var name)) continue;
                    // If a name Regex filter is applied, and it does not match, continue
                    if (!string.IsNullOrEmpty(nameRegexFilter) && !nameRegex.IsMatch(name)) continue;

                    var progress = x / (float)fileInfos.Count;
                    progressBar.Update($"[Loading] {name}", progress: progress);

                    var assetTypeInstanceField = am.GetTypeInstance(resourcesInst, assetFileInfo).GetBaseField();

                    var ext = am.GetExtAsset(resourcesInst, 0, assetFileInfo.index);

                    // Find name, path and fileId of each asset referenced directly and indirectly by assetFileInfo including itself
                    targetAssets = targetAssets.Concat(resourcesInst.GetDependentAssetIds(assetTypeInstanceField, am)
                                                                    .Prepend((ext, null, name, "resources.assets", 0, assetFileInfo.index, 0)));
                }
            }

            return targetAssets;
        }
        private void InitializeAssetTools(string resourcesFilePath, string path, out BundleFileInstance bun, out AssetsFileInstance bundleAssetsFile, out AssetsFileInstance resourcesInst, out AssetFileInfoEx assetBundleAsset, out AssetExternal assetBundleExtAsset, out AssetTypeValueField bundleBaseField)
        {
            am = new AssetsManager();
            //Load bundle file and its AssetsFile
            bun = am.LoadBundleFile(path, true);
            bundleAssetsFile = am.LoadAssetsFileFromBundle(bun, 0);

            // Load assets files from Resources
            resourcesInst = am.LoadAssetsFile(resourcesFilePath, true);

            //Load AssetBundle asset from Bundle AssetsFile so that we can update its data later
            assetBundleAsset = bundleAssetsFile.table.GetAssetsOfType((int)AssetClassID.AssetBundle)[0];
            assetBundleExtAsset = am.GetExtAsset(bundleAssetsFile, 0, assetBundleAsset.index);

            //load data for classes
            var classDataPath = Path.Combine("Packages", "com.passivepicasso.bundlekit", "Library", "classdata.tpk");
            am.LoadClassPackage(classDataPath);
            am.LoadClassDatabaseFromPackage(resourcesInst.file.typeTree.unityVersion);

            bundleBaseField = assetBundleExtAsset.instance.GetBaseField();
        }

        private void InitializeContainers(out List<AssetsReplacer> assetsReplacers, out List<string> contexts, out IEnumerable<AssetData> targetAssets, out List<AssetTypeValueField> newContainerChildren, out Regex nameRegex)
        {
            assetsReplacers = new List<AssetsReplacer>();
            contexts = new List<string>();
            targetAssets = Enumerable.Empty<AssetData>();
            newContainerChildren = new List<AssetTypeValueField>();
            nameRegex = new Regex(nameRegexFilter);
        }
        private void InitializePaths(out string resourcesFilePath, out string ggmPath, out string fileName, out string bundlePath, out string dataDirectoryPath)
        {
            var settings = ThunderKitSetting.GetOrCreateSettings<ThunderKitSettings>();
            dataDirectoryPath = Path.Combine(settings.GamePath, $"{Path.GetFileNameWithoutExtension(settings.GameExecutable)}_Data");
            resourcesFilePath = Path.Combine(dataDirectoryPath, "resources.assets");
            ggmPath = Path.Combine(dataDirectoryPath, "globalgamemanagers");
            fileName = Path.GetFileName(outputAssetBundlePath);
            bundlePath = AssetDatabase.GetAssetPath(bundle);
        }
    }
}