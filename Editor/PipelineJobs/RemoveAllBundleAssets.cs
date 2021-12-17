using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ThunderKit.Core.Pipelines;
using UnityEditor;

namespace BundleKit.PipelineJobs
{
    [PipelineSupport(typeof(Pipeline))]
    public class RemoveAllBundleAssets : PipelineJob
    {
        public DefaultAsset bundle;
        public string outputAssetBundlePath;

        public override Task Execute(Pipeline pipeline)
        {
            var am = new AssetsManager();
            var path = AssetDatabase.GetAssetPath(bundle);

            var fileStream = File.OpenRead(path);
            var bun = am.LoadBundleFile(fileStream, true);
            var bundleAssetsFile = am.LoadAssetsFileFromBundle(bun, 0);
            var resSFile = bun.file.bundleInf6.dirInf.FirstOrDefault(dir => dir.name.Contains("resS"));

            am.LoadClassPackage("classdata.tpk");
            var assetsReplacers = new List<AssetsReplacer>();
            var bundleReplacers = new List<BundleReplacer>();
            var referenceContext = "Removed Assets\r\n";

            foreach (var assetFileInfo in bundleAssetsFile.table.assetFileInfo)// m_Container.children
            {
                if (!assetFileInfo.ReadName(bundleAssetsFile.file, out var name)) continue;
                if ((AssetClassID)assetFileInfo.curFileType == AssetClassID.AssetBundle) continue;

                long pathId = assetFileInfo.index;

                var type = (AssetClassID)assetFileInfo.curFileType;
                referenceContext += $"1. ({type}) \"{name}\" {{FileID: 0, PathID: {pathId} }}\r\n";

                var remover = new AssetsRemover(0, pathId, (int)type);
                assetsReplacers.Add(remover);
            }

            pipeline.Log(LogLevel.Information, "Removing bundle assets", referenceContext);

            byte[] newAssetData;
            using (var bundleStream = new MemoryStream())
            using (var writer = new AssetsFileWriter(bundleStream))
            {
                bundleAssetsFile.file.Write(writer, 0, assetsReplacers, 0);
                newAssetData = bundleStream.ToArray();
            }
            var resSRemover = new BundleRemover(resSFile.name, true);
            bundleReplacers.Add(resSRemover);
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