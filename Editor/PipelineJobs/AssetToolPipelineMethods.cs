using AssetsTools.NET;
using AssetsTools.NET.Extra;
using BundleKit.Assets;
using BundleKit.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ThunderKit.Common.Logging;
using UnityEditor.Build.Pipeline.Utilities;

namespace BundleKit.PipelineJobs
{
    public static class AssetToolPipelineMethods
    {
        public static AssetsFileInstance GetAssetsInst(this AssetsManager am, string assetsFilePath)
        {
            // Load assets files from Resources
            var assetsFileInst = am.LoadAssetsFile(assetsFilePath, false);


            return assetsFileInst;
        }
        public static void PrepareNewBundle(this AssetsManager am, string path, out BundleFileInstance bun, out AssetsFileInstance bundleAssetsFile, out AssetExternal assetBundleExtAsset)
        {
            //Load bundle file and its AssetsFile
            bun = am.LoadBundleFile(path, true);
            bundleAssetsFile = am.LoadAssetsFileFromBundle(bun, 0);

            //Load AssetBundle asset from Bundle AssetsFile so that we can update its data later
            var assetBundleAsset = bundleAssetsFile.table.GetAssetsOfType((int)AssetClassID.AssetBundle)[0];
            assetBundleExtAsset = am.GetExtAsset(bundleAssetsFile, 0, assetBundleAsset.index);
        }
        public static IEnumerable<AssetData> CollectAssets(AssetsManager am, AssetsFileInstance assetsFileInst, HashSet<AssetID> visited, Regex[] nameRegex, AssetClassID assetClass, UpdateLog Update)
        {
            // Iterate over all requested Class types and collect the data required to copy over the required asset information
            // This step will recurse over dependencies so all required assets will become available from the resulting bundle
            Update("Mapping PathIds to Resource Paths", log: false);
            var targetAssets = Enumerable.Empty<AssetData>();
            var fileInfos = assetsFileInst.table.GetAssetsOfType((int)assetClass);
            for (var x = 0; x < fileInfos.Count; x++)
            {
                var assetFileInfo = fileInfos[x];

                var name = AssetHelper.GetAssetNameFast(assetsFileInst.file, am.classFile, assetFileInfo);
                // If a name Regex filter is applied, and it does not match, continue
                int i = 0;
                for (; i < nameRegex.Length; i++)
                    if (nameRegex[i] != null && nameRegex[i].IsMatch(name))
                        break;
                if (nameRegex.Length != 0 && i == nameRegex.Length) continue;

                var assetId = assetsFileInst.ConvertToAssetID(0, assetFileInfo.index);
                if (visited.Contains(assetId)) continue;
                visited.Add(assetId);

                var ext = am.GetExtAsset(assetsFileInst, 0, assetFileInfo.index);
                if (ext.file.name.Contains("unity_builtin_extra"))
                    continue;

                name = ext.GetName(am);
                Update($"Scanning Assets ({x} / {fileInfos.Count})", $"[Loading] ({(AssetClassID)assetFileInfo.curFileType}) {name}", x / (float)fileInfos.Count, false);

                // Find name, path and fileId of each asset referenced directly and indirectly by assetFileInfo including itself
                targetAssets = targetAssets.Concat(assetsFileInst.GetDependentAssetIds(visited, ext.instance.GetBaseField(), am, Update, true)
                                           .Prepend((ext, name, assetsFileInst.name, 0, assetFileInfo.index, 0)));
            }

            return targetAssets;
        }

        public static void AddDependency(this AssetsFileInstance assetsFileInst, AssetsFileDependency assetsFileDependency)
        {
            var dependencies = assetsFileInst.file.dependencies.dependencies;

            var assetsFilePath = assetsFileDependency.assetPath;
            var fixedPath = assetsFilePath;
            if (assetsFilePath != Extensions.unityBuiltinExtra && assetsFilePath != Extensions.unityDefaultResources)
            {
                fixedPath = $"{assetsFilePath}reference";
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
        public static void UpdatePreloadTable(this AssetsFileInstance assetsFileInst, AssetsManager am, AssetTypeValueField preloadTableArray, List<AssetTypeValueField> containerArray, HashSet<AssetID> visited, UpdateLog log)
        {
            var children = new List<AssetTypeValueField>();
            var count = assetsFileInst.file.dependencies.dependencyCount + 1;
            int preloadIndex = 0;
            // Setup preload table
            foreach (var assetIndexField in containerArray)
            {
                var name = assetIndexField.GetValue("first").AsString();
                long pathId = assetIndexField.GetValue("second/asset/m_PathID").AsInt64();
                if (pathId == 0) continue;
                var assetExt = am.GetExtAsset(assetsFileInst, 0, pathId);
                var baseField = assetExt.instance.GetBaseField();

                int size = 0;
                foreach (var dependency in assetExt.file.GetDependentAssetIds(visited, baseField, am, log, true))
                {
                    var entry = ValueBuilder.DefaultValueFieldFromArrayTemplate(preloadTableArray);
                    entry.SetValue("m_FileID", dependency.FileId);
                    entry.SetValue("m_PathID", dependency.PathId);
                    children.Add(entry);
                    size++;
                }
                assetIndexField.SetValue("second/preloadIndex", preloadIndex);
                assetIndexField.SetValue("second/preloadSize", size);
                preloadIndex += size;

            }
            if (children.Any())
                preloadTableArray.SetChildrenList(children.ToArray());
        }
    }
}