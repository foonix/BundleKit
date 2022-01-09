using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System.Collections.Generic;
using System.IO;
using UnityEditor.Build.Pipeline.Utilities;

namespace BundleKit.Utility
{
    public static class AssetToolPipelineMethods
    {
        public static (BundleFileInstance bun, AssetsFileInstance bundleAssetsFile, AssetExternal assetBundleExtAsset) LoadBundle(this AssetsManager am, string path)
        {
            //Load bundle file and its AssetsFile
            var bun = am.LoadBundleFile(path, true);
            var bundleAssetsFile = am.LoadAssetsFileFromBundle(bun, 0);

            //Load AssetBundle asset from Bundle AssetsFile so that we can update its data later
            var assetBundleAsset = bundleAssetsFile.table.GetAssetsOfType((int)AssetClassID.AssetBundle)[0];
            var assetBundleExtAsset = am.GetExtAsset(bundleAssetsFile, 0, assetBundleAsset.index);

            return (bun, bundleAssetsFile, assetBundleExtAsset);
        }

        public static void AddDependency(this AssetsFileInstance assetsFileInst, AssetsFileDependency assetsFileDependency)
        {
            var dependencies = assetsFileInst.file.dependencies.dependencies;

            var assetsFilePath = assetsFileDependency.assetPath;
            var fixedPath = assetsFilePath;
            if (assetsFilePath != Extensions.unityBuiltinExtra && assetsFilePath != Extensions.unityDefaultResources)
            {
                fixedPath = Path.GetFileNameWithoutExtension(assetsFilePath);
                var cabName = $"cab-{HashingMethods.Calculate<MD4>(fixedPath)}";
                fixedPath = $"archive:/{cabName}/{cabName}";
            }

            dependencies.Add(
                new AssetsFileDependency
                {
                    assetPath = fixedPath,
                    originalAssetPath = fixedPath,
                    bufferedPath = string.Empty,
                    guid = assetsFileDependency.guid,
                    type = assetsFileDependency.type
                }
            );
            assetsFileInst.file.dependencies.dependencyCount = dependencies.Count;
            assetsFileInst.file.dependencies.dependencies = dependencies;
        }
        public static void AddDependency(this AssetsFileInstance assetsFileInst, string dependency)
        {
            var dependencies = assetsFileInst.file.dependencies;

            dependencies.dependencies.Add(new AssetsFileDependency
            {
                assetPath = dependency,
                originalAssetPath = dependency,
                bufferedPath = string.Empty
            });

            dependencies.dependencyCount = dependencies.dependencies.Count;
        }

        public static void UpdateAssetBundleDependencies(AssetTypeValueField bundleBaseField, List<AssetsFileDependency> dependencies)
        {
            var dependencyArray = bundleBaseField.GetField("m_Dependencies/Array");
            var dependencyFieldChildren = new List<AssetTypeValueField>();

            foreach (var dep in dependencies)
            {
                var depTemplate = ValueBuilder.DefaultValueFieldFromArrayTemplate(dependencyArray);
                depTemplate.GetValue().Set(dep.assetPath);
                dependencyFieldChildren.Add(depTemplate);
            }
            dependencyArray.SetChildrenList(dependencyFieldChildren.ToArray());
        }
    }
}