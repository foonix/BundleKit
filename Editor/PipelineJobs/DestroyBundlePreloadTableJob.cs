using AssetsTools.NET;
using AssetsTools.NET.Extra;
using BundleKit.Bundles;
using BundleKit.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ThunderKit.Core.Pipelines;
using UnityEditor;

namespace BundleKit.PipelineJobs
{
    [PipelineSupport(typeof(Pipeline))]
    public class DestroyBundlePreloadTableJob : PipelineJob
    {
        public AssetsReferenceBundle bundle;
        public string outputAssetBundlePath;

        public override Task Execute(Pipeline pipeline)
        {

            var am = new AssetsManager();
            var path = AssetDatabase.GetAssetPath(bundle);

            am.PrepareNewBundle(path, out var bun, out var bundleAssetsFile, out var assetBundleExtAsset);

            var classDataPath = Path.Combine("Packages", "com.passivepicasso.bundlekit", "Library", "classdata.tpk");
            am.LoadClassPackage(classDataPath);

            var assetsReplacers = new List<AssetsReplacer>();
            var bundleReplacers = new List<BundleReplacer>();
            var referenceContext = "Removed Assets\r\n";
            var bundleBaseField = assetBundleExtAsset.instance.GetBaseField();
            var preloadTableArray = bundleBaseField.GetField("m_PreloadTable/Array");
            preloadTableArray.SetChildrenList(Array.Empty<AssetTypeValueField>());

            var containerChildren = bundleBaseField.GetField("m_Container/Array").GetChildrenList();
            foreach(var child in containerChildren)
            {
                child.SetValue("second/preloadIndex", 0);
                child.SetValue("second/preloadSize", 0);
            }

            var newAssetBundleBytes = bundleBaseField.WriteToByteArray();
            assetsReplacers.Add(new AssetsReplacerFromMemory(0, assetBundleExtAsset.info.index, (int)assetBundleExtAsset.info.curFileType, 0xFFFF, newAssetBundleBytes));

            pipeline.Log(LogLevel.Information, "Removing bundle assets", referenceContext);

            byte[] newAssetData;
            using (var bundleStream = new MemoryStream())
            using (var writer = new AssetsFileWriter(bundleStream))
            {
                bundleAssetsFile.file.Write(writer, 0, assetsReplacers, 0);
                newAssetData = bundleStream.ToArray();
            }
            var bundleReplacer = new BundleReplacerFromMemory(bundleAssetsFile.name, bundleAssetsFile.name, true, newAssetData, -1);
            bundleReplacers.Add(bundleReplacer);

            using (var file = File.OpenWrite(outputAssetBundlePath))
            using (var writer = new AssetsFileWriter(file))
                bun.file.Write(writer, bundleReplacers);

            pipeline.Log(LogLevel.Information, "Removed bundle assets", referenceContext);

            return Task.CompletedTask;
        }
    }
}
