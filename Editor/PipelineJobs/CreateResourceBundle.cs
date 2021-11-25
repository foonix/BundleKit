using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ThunderKit.Common.Logging;
using ThunderKit.Core.Paths;
using ThunderKit.Core.Pipelines;
using UnityEditor;
using BundleKit.Assets;

namespace BundleKit.PipelineJobs
{
    [PipelineSupport(typeof(Pipeline))]
    public class CreateResourceBundle : PipelineJob
    {
        public DefaultAsset bundle;
        public string dataDirectory;
        public string outputAssetBundlePath;
        public string nameRegexFilter = string.Empty;
        public AssetClassID[] classes;

        private AssetsManager am;
        public override Task Execute(Pipeline pipeline)
        {
            using (var progressBar = new ProgressBar("Constructing AssetBundle"))
            {
                am = new AssetsManager();

                var assetsReplacers = new List<AssetsReplacer>();
                var contexts = new List<string>();

                var nameRegex = new Regex(nameRegexFilter);

                // Prepare paths to files required to build bundle
                var dataDirectoryPath = PathReference.ResolvePath(dataDirectory, pipeline, this);
                var resourcesFilePath = Path.Combine(dataDirectoryPath, "resources.assets");
                var resourcesresSFilePath = Path.Combine(dataDirectoryPath, "resources.assets.resS");
                var ggmPath = Path.Combine(dataDirectoryPath, "globalgamemanagers");
                var fileName = Path.GetFileName(outputAssetBundlePath);
                var path = AssetDatabase.GetAssetPath(bundle);

                //Load bundle file and its AssetsFile
                var bun = am.LoadBundleFile(path, true);
                var bundleAssetsFile = am.LoadAssetsFileFromBundle(bun, 0);

                // Load assets files from Resources and GlobalGameManagers
                var resourcesInst = am.LoadAssetsFile(resourcesFilePath, true);
                var ggm = am.LoadAssetsFile(ggmPath, true);

                // Get information about the build environment
                var unityVersion = resourcesInst.file.typeTree.unityVersion;
                var formatVer = bundleAssetsFile.file.header.format;
                var typeTreeVer = bundleAssetsFile.file.typeTree.version;

                //Load AssetBundle asset from Bundle AssetsFile so that we can update its data later
                var assetBundleAsset = bundleAssetsFile.table.GetAssetsOfType((int)AssetClassID.AssetBundle)[0];
                var assetBundleExtAsset = am.GetExtAsset(bundleAssetsFile, 0, assetBundleAsset.index);

                //load data for classes
                am.LoadClassPackage("classdata.tpk");
                am.LoadClassDatabaseFromPackage(unityVersion);

                //// Create a text asset to store information that will be used to modified bundles built by users to redirect references to the original source files
                //var templateField = new AssetTypeTemplateField();
                //var cldbType = AssetHelper.FindAssetClassByName(am.classFile, "TextAsset");
                //templateField.FromClassDatabase(am.classFile, cldbType, 0);
                //var baseField = ValueBuilder.DefaultValueFieldFromTemplate(templateField);
                //baseField.Get("m_Name").GetValue().Set("MyCoolTextAsset");
                //baseField.Get("m_Script").GetValue().Set("I have some sick text");

                // Iterate over all requested Class types and collect the data required to copy over the required asset information
                // This step will recurse over dependencies so all required assets will become available from the resulting bundle
                progressBar.Update(title: "Mapping PathIds to Resource Paths");
                var targetAssets = Enumerable.Empty<AssetData>();
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

                        var ext = am.GetExtAsset(resourcesInst, 0, assetFileInfo.index);

                        // Find name, path and fileId of each asset referenced directly and indirectly by assetFileInfo including itself
                        var assetTypeInstanceField = am.GetTypeInstance(resourcesInst, assetFileInfo).GetBaseField();
                        targetAssets = targetAssets.Concat(resourcesInst.GetDependentAssetIds(am, assetTypeInstanceField)
                                                                        .Prepend((ext, null, name, 0, assetFileInfo.index, 0)));

                    }
                }

                // Attempt to populate the AssetBundle asset's m_Container so it has a proper list of asssets contained by the bundle
                // Update bundle assets name and bundle name to the name specified in outputAssetBundlePath
                var bundleBaseField = assetBundleExtAsset.instance.GetBaseField();
                bundleBaseField.Get("m_Name").GetValue().Set(fileName);
                bundleBaseField.Get("m_AssetBundleName").GetValue().Set(fileName);
                var containerArray = bundleBaseField.Get("m_Container").Get("Array");
                var newContainerChildren = new List<AssetTypeValueField>();

                var context = string.Empty;
                var realizedAssetTargets = targetAssets.ToArray();
                foreach (var (asset, pptr, assetName, fileId, pathId, depth) in realizedAssetTargets)
                {
                    if (depth == 0)
                    {
                        if (!string.IsNullOrEmpty(context)) contexts.Add(context);

                        context = $"{assetName}\r\n";
                    }

                    var assetBaseField = asset.instance.GetBaseField();
                    assetBaseField.SetPPtrsFileIdZero(asset.file);

                    var otherBytes = asset.instance.WriteToByteArray();
                    var currentAssetReplacer = new AssetsReplacerFromMemory(0, pathId, (int)asset.info.curFileType,
                                                                            AssetHelper.GetScriptIndex(asset.file.file, asset.info),
                                                                            otherBytes);
                    assetsReplacers.Add(currentAssetReplacer);

                    // Use m_Container to construct an blank element for it
                    var pair = ValueBuilder.DefaultValueFieldFromArrayTemplate(containerArray);
                    //Name the asset identified by this element
                    pair.Get("first").GetValue().Set(assetName);

                    //Get fields for populating index and 
                    var second = pair.Get("second");
                    var assetField = second.Get("asset");

                    //We are not constructing a preload table, so we are setting these all to zero
                    second.SetValue("preloadIndex", 0);
                    second.SetValue("preloadSize", 0);

                    // Update the fileId and PathID so that asset can be located within the bundle
                    // We zero out the fileId because the asset is in the local file, not a dependent file
                    assetField.SetValue("m_FileID", 0);
                    assetField.SetValue("m_PathID", pathId);

                    newContainerChildren.Add(pair);

                    context += GenerateContextLog(asset, depth);
                }
                if (!string.IsNullOrEmpty(context)) contexts.Add(context);

                bundleBaseField.Get("m_Container").Get("Array").SetChildrenList(newContainerChildren.ToArray());

                bundleBaseField.Get("m_PreloadTable").Get("Array").SetChildrenList(Array.Empty<AssetTypeValueField>());

                //Save changes for building new bundle file
                var newAssetBundleBytes = bundleBaseField.WriteToByteArray();
                assetsReplacers.Add(new AssetsReplacerFromMemory(0, assetBundleAsset.index, (int)assetBundleAsset.curFileType, 0xFFFF, newAssetBundleBytes));

                if (File.Exists(outputAssetBundlePath)) File.Delete(outputAssetBundlePath);

                byte[] newAssetData;
                using (var bundleStream = new MemoryStream())
                using (var writer = new AssetsFileWriter(bundleStream))
                {
                    //We need to order the replacers by their pathId for Unity to be able to read the Bundle correctly.
                    bundleAssetsFile.file.Write(writer, 0, assetsReplacers.OrderBy(repl => repl.GetPathID()).ToList(), 0);
                    newAssetData = bundleStream.ToArray();
                }

                using (var file = File.OpenWrite(outputAssetBundlePath))
                using (var writer = new AssetsFileWriter(file))
                {
                    var assetsFileName = $"CAB-{GUID.Generate()}";
                    var resFileName = $"{assetsFileName}.resS";
                    bun.file.Write(writer, new List<BundleReplacer>
                    {
                        new BundleReplacerFromMemory(bundleAssetsFile.name, assetsFileName, true, newAssetData, newAssetData.Length),
                    });
                }

                pipeline.Log(LogLevel.Information, $"Finished Building Bundle", contexts.ToArray());
            }
            return Task.CompletedTask;
        }

        private static string GenerateContextLog(AssetExternal asset, int depth)
        {
            var depthStr = depth == 0 ? string.Empty : Enumerable.Repeat("  ", depth).Aggregate((a, b) => $"{a}{b}");
            var listDelim = "* ";// depth % 2 == 0 ? "* " : "- ";
            return $"{depthStr}{listDelim}({(AssetClassID)asset.info.curFileType}) Name: \"{asset.GetName()}\"\r\n";
        }

    }
}