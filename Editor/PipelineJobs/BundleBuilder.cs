using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ThunderKit.Core.Paths;
using ThunderKit.Core.Pipelines;
using UnityEditor;
using BundleKit.Assets;

namespace BundleKit.PipelineJobs
{
    [PipelineSupport(typeof(Pipeline))]
    public class BundleBuilder : PipelineJob
    {
        public DefaultAsset bundle;

        public string dataDirectory;
        public string outputAssetBundlePath;

        public override Task Execute(Pipeline pipeline)
        {
            var am = new AssetsManager();
            var path = AssetDatabase.GetAssetPath(bundle);

            var dataDirectoryPath = PathReference.ResolvePath(dataDirectory, pipeline, this);
            var resourcesFilePath = Path.Combine(dataDirectoryPath, "resources.assets");
            var ggmPath = Path.Combine(dataDirectoryPath, "globalgamemanagers");

            var resourceFile = am.LoadAssetsFile(resourcesFilePath, true);
            var ggm = am.LoadAssetsFile(ggmPath, true);

            var fileStream = File.OpenRead(path);
            var bun = am.LoadBundleFile(fileStream, false);
            var assetInst = am.LoadAssetsFileFromBundle(bun, 0, true);

            var unityVersion = ggm.file.typeTree.unityVersion;

            am.LoadClassPackage("classdata.tpk");
            am.LoadClassDatabaseFromPackage(unityVersion);

            var resourcesLookupTable = new Dictionary<string, long>();
            var bundleLookupTable = new Dictionary<string, long>();

            var resourceInfo = ggm.table.GetAssetsOfType((int)AssetClassID.ResourceManager)[0];
            var resourceBaseField = am.GetTypeInstance(ggm, resourceInfo).GetBaseField();
            var resourcesContainer = resourceBaseField.Get("m_Container").Get("Array");

            foreach (var data in resourcesContainer.children)
            {
                var name = data[0].GetValue().AsString();
                var pathId = data[1].Get("m_PathID").GetValue().AsInt64();
                if (name.Contains("shaders/"))
                    resourcesLookupTable[name] = pathId;
            }

            var abInfo = assetInst.table.GetAssetsOfType((int)AssetClassID.AssetBundle)[0];
            var abBf = am.GetTypeInstance(assetInst, abInfo).GetBaseField();
            var bundleContainer = abBf.Get("m_Container").Get("Array");
            foreach (var data in bundleContainer.children)
            {
                var name = data[0].GetValue().AsString();
                var pathId = data[1].Get("asset").Get("m_PathID").GetValue().AsInt64();
                if (name.Contains("shaders/"))
                    bundleLookupTable[name] = pathId;
            }

            var bundleAssetsFile = bun.assetsFiles[0];
            var assetReplacers = new List<AssetsReplacer>();

            foreach (var kvp in bundleLookupTable)
            {
                var trimmedName = kvp.Key.Remove(0, "assets/resources/".Length).Replace(".asset", "");

                if (!resourcesLookupTable.ContainsKey(trimmedName)) continue;
                var assetId = resourcesLookupTable[trimmedName];
                var asset = am.GetExtAsset(resourceFile, 0, assetId);
                var assetBytes = asset.instance.WriteToByteArray();
                var assetReplacer = new AssetsReplacerFromMemory(0, kvp.Value, (int)asset.info.curFileType, 0xFFFF, assetBytes);
                assetReplacers.Add(assetReplacer);
            }

            byte[] newAssetData;
            using (var stream = new MemoryStream())
            using (var writer = new AssetsFileWriter(stream))
            {
                bundleAssetsFile.file.Write(writer, 0, assetReplacers, 0);
                newAssetData = stream.ToArray();
            }

            var bundleReplacer = new BundleReplacerFromMemory(bundleAssetsFile.name, bundleAssetsFile.name, true, newAssetData, -1);
            using (var file = File.OpenWrite(outputAssetBundlePath))
            using (var writer = new AssetsFileWriter(file))
            {
                bun.file.Write(writer, new List<BundleReplacer> { bundleReplacer });
            }

            return Task.CompletedTask;
        }
    }
}