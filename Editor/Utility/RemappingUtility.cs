using AssetsTools.NET;
using AssetsTools.NET.Extra;
using BundleKit.Building.Contexts;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ThunderKit.Core.Data;
using UnityEditor;

namespace BundleKit.Utility
{
    public static class RemappingUtility
    {
        public static void RemapBundle(string bundleArtifactPath, AssetBundleBuild[] builds, RemapContext remapContext, Compression buildCompression)
        {
            var assetsReplacers = new List<AssetsReplacer>();
            var bundleReplacers = new List<BundleReplacer>();
            foreach (var build in builds)
            {
                assetsReplacers.Clear();
                bundleReplacers.Clear();
                //Load AssetBundle using AssetTools.Net
                //Modify AssetBundles by removing assets named (Asset Reference) and all their dependencies.
                var am = new AssetsManager();
                var path = Path.Combine(bundleArtifactPath, build.assetBundleName);
                var temppath = Path.Combine(bundleArtifactPath, $"{build.assetBundleName}.tmp");

                if (File.Exists(temppath)) File.Delete(temppath);
                File.Move(path, temppath);

                using (var stream = File.OpenRead(temppath))
                {
                    var bun = am.LoadBundleFile(stream);
                    var fileCount = bun.file.NumFiles;

                    // Remove all (Asset Reference)s from bundles, then update all references to those assets with references to the original assets file.
                    // Additionally update bundle dependencies to include Resources.assets as a dependency
                    for (int i = 0; i < fileCount; i++)
                    {
                        var assetsFile = am.LoadAssetsFileFromBundle(bun, i, true);
                        if (assetsFile == null) continue; // This will occur if the index of this file is for a resS file which we don't need to process here.

                        UpdateAssetsFileDependencies(assetsFile);

                        var bundleAssets = assetsFile.table.GetAssetsOfType((int)AssetClassID.AssetBundle);
                        if (bundleAssets.Count > 0)
                        {
                            var assetBundleAsset = bundleAssets[0];
                            UpdateAssetBundleDependencies(assetsReplacers, am, assetsFile, assetBundleAsset);
                        }

                        RemapExternalReferences(assetsReplacers, am, assetsFile, remapContext);

                        assetsReplacers = assetsReplacers.OrderBy(repl => repl.GetPathID()).ToList();
                        byte[] newAssetData;
                        using (var bundleStream = new MemoryStream())
                        using (var writer = new AssetsFileWriter(bundleStream))
                        {
                            assetsFile.file.Write(writer, 0, assetsReplacers);
                            newAssetData = bundleStream.ToArray();
                        }
                        var bundleReplacer = new BundleReplacerFromMemory(assetsFile.name, assetsFile.name, true, newAssetData, i);
                        bundleReplacers.Add(bundleReplacer);
                    }

                    #region Update preload table before updating anything else
                    //for (int i = 0; i < fileCount; i++)
                    //{
                    //    assetsFile = am.LoadAssetsFileFromBundle(bun, i, true);
                    //    if (assetsFile == null) continue; // This will occur if the index of this file is for a resS file which we don't need to process here.

                    //    var bundleAssets = assetsFile.table.GetAssetsOfType((int)AssetClassID.AssetBundle);
                    //    if (bundleAssets.Count > 0)
                    //    {
                    //        var assetBundleAsset = bundleAssets[0];
                    //        UpdatePreloadTable(am, assetsFile, assetBundleAsset, remapContext);
                    //    }
                    //}
                    #endregion

                    //write out chagnes to bundle
                    using (var file = File.OpenWrite(path))
                    using (var writer = new AssetsFileWriter(file))
                        bun.file.Write(writer, bundleReplacers);
                }

                if (File.Exists(temppath)) File.Delete(temppath);
                if (buildCompression != Compression.Uncompressed)
                {
                    File.Move(path, temppath);
                    using (var stream = File.OpenRead(temppath))
                    {
                        var bun = am.LoadBundleFile(stream);
                        using (var file = File.OpenWrite(path))
                        using (var writer = new AssetsFileWriter(file))
                            switch (buildCompression)
                            {
                                case Compression.LZMA:
                                    bun.file.Pack(bun.file.reader, writer, AssetBundleCompressionType.LZMA);
                                    break;
                                case Compression.LZ4:
                                    bun.file.Pack(bun.file.reader, writer, AssetBundleCompressionType.LZ4);
                                    break;
                            }
                    }
                    if (File.Exists(temppath)) File.Delete(temppath);
                }
            }
        }

        public static void UpdateAssetsFileDependencies(AssetsFileInstance assetsFileInst)
        {
            var settings = ThunderKitSetting.GetOrCreateSettings<ThunderKitSettings>();
            var dependencies = assetsFileInst.file.dependencies.dependencies;

            //Copy dependencies array from resources.assets and add dependency to resources.assets
            string fullResourcesPath = $"{settings.GamePath}\\{Path.GetFileNameWithoutExtension(settings.GameExecutable)}_Data\\resources.assets";
            dependencies.Add(
                new AssetsFileDependency
                {
                    assetPath = fullResourcesPath,
                    originalAssetPath = fullResourcesPath,
                    bufferedPath = string.Empty
                }
            );
            assetsFileInst.file.dependencies.dependencyCount = dependencies.Count;
            assetsFileInst.file.dependencies.dependencies = dependencies;
        }

        public static void UpdateAssetBundleDependencies(List<AssetsReplacer> assetsReplacers, AssetsManager am, AssetsFileInstance assetsFileInst, AssetFileInfoEx assetBundleInfo)
        {
            var assetBundleExtAsset = am.GetExtAsset(assetsFileInst, 0, assetBundleInfo.index);
            var bundleBaseField = assetBundleExtAsset.instance.GetBaseField();
            var dependencyArray = bundleBaseField.Get("m_Dependencies", "Array");
            var dependencyFieldChildren = new List<AssetTypeValueField>();

            foreach (var dep in assetsFileInst.file.dependencies.dependencies)
            {
                var depTemplate = ValueBuilder.DefaultValueFieldFromArrayTemplate(dependencyArray);
                depTemplate.GetValue().Set(dep.assetPath);
                dependencyFieldChildren.Add(depTemplate);
            }
            dependencyArray.SetChildrenList(dependencyFieldChildren.ToArray());

            var newAssetBundleBytes = bundleBaseField.WriteToByteArray();
            assetsReplacers.Add(new AssetsReplacerFromMemory(0, assetBundleInfo.index, (int)assetBundleInfo.curFileType, 0xFFFF, newAssetBundleBytes));
        }

        public static void RemapExternalReferences(List<AssetsReplacer> assetsReplacers, AssetsManager am, AssetsFileInstance assetsFileInst, RemapContext remapContext)
        {
            var dependencies = assetsFileInst.file.dependencies.dependencies;

            var remap = remapContext.AssetMaps
                .Where(map => map.name.Equals(assetsFileInst.name))
                .ToDictionary(map => (map.BundlePointer.fileId, map.BundlePointer.pathId),
                              map => (fileId: dependencies.Count, map.ResourcePointer.pathId));

            var dependencyPaths = new HashSet<string>();

            foreach (var assetFileInfo in assetsFileInst.table.assetFileInfo)
            {
                if (!assetFileInfo.ReadName(assetsFileInst.file, out var name))
                    continue;

                if (name.Contains("(Asset Reference)"))
                {
                    long pathId = assetFileInfo.index;
                    var type = (AssetClassID)assetFileInfo.curFileType;
                    var assetExt = am.GetExtAsset(assetsFileInst, 0, assetFileInfo.index);
                    AssetTypeValueField baseField = assetExt.instance.GetBaseField();

                    //Collect the dependency;
                    dependencyPaths.Add(assetExt.file.path);

                    //add remover for this asset
                    if (!assetsReplacers.Any(ar => ar.GetPathID() == pathId))
                        assetsReplacers.Add(new AssetsRemover(0, pathId, (int)type));

                    //add remover for all this asset's dependencies
                    foreach (var (asset, pptr, assetName, assetFileName, fileId, pathID, depth) in assetExt.file.GetDependentAssetIds(baseField, am))
                    {
                        //Collect the dependency;
                        dependencyPaths.Add(asset.file.path);
                        if (!assetsReplacers.Any(ar => ar.GetPathID() == pathID && ar.GetFileID() == fileId))
                            assetsReplacers.Add(new AssetsRemover(fileId, pathID, (int)asset.info.curFileType,
                                                    AssetHelper.GetScriptIndex(asset.file.file, asset.info)));
                    }
                }
                else if (name.Contains("(Custom Asset)"))
                {
                    var assetExt = am.GetExtAsset(assetsFileInst, 0, assetFileInfo.index);
                    AssetTypeValueField baseField = assetExt.instance.GetBaseField();

                    //add remover for all dependencies on this asset
                    foreach (var (asset, pptr, assetName, assetFileName, fileId, pathID, depth) in assetExt.file.GetDependentAssetIds(baseField, am))
                    {
                        //Collect the dependency;
                        dependencyPaths.Add(asset.file.path);
                        if (!assetsReplacers.Any(ar => ar.GetPathID() == pathID && ar.GetFileID() == fileId))
                            assetsReplacers.Add(new AssetsRemover(fileId, pathID, (int)asset.info.curFileType,
                                                    AssetHelper.GetScriptIndex(asset.file.file, asset.info)));
                    }
                }
            }

            foreach (var assetFileInfo in assetsFileInst.table.assetFileInfo)
            {
                if (assetsReplacers.Any(ar => ar.GetPathID() == assetFileInfo.index))
                    continue;

                var asset = am.GetExtAsset(assetsFileInst, 0, assetFileInfo.index);
                AssetTypeValueField assetBaseField = asset.instance.GetBaseField();
                assetBaseField.RemapPPtrs(remap);
                var otherBytes = asset.instance.WriteToByteArray();
                var currentAssetReplacer = new AssetsReplacerFromMemory(0, assetFileInfo.index, (int)asset.info.curFileType,
                                                                        AssetHelper.GetScriptIndex(asset.file.file, asset.info),
                                                                        otherBytes);
                assetsReplacers.Add(currentAssetReplacer);
            }


        }

        public static void UpdatePreloadTable(AssetsManager am, AssetsFileInstance assetsFileInst, AssetFileInfoEx assetBundleInfo, RemapContext remapContext)
        {
            var assetBundleExtAsset = am.GetExtAsset(assetsFileInst, 0, assetBundleInfo.index);
            var bundleBaseField = assetBundleExtAsset.instance.GetBaseField();
            var preloadTableArray = bundleBaseField.GetField("m_PreloadTable/Array");
            var preloadFieldChildren = new List<AssetTypeValueField>();

            var count = assetsFileInst.file.dependencies.dependencyCount + 1;

            var remap = remapContext.AssetMaps
                .Where(map => map.name.Equals(assetsFileInst.name))
                .ToDictionary(map => (map.BundlePointer.fileId, map.BundlePointer.pathId),
                              map => (fileId: count + 1, map.ResourcePointer.pathId));

            var containerArray = bundleBaseField.GetField("m_Container/Array");
            // Setup preload table
            foreach (var assetIndexField in containerArray.GetChildrenList())
            {
                var name = assetIndexField.GetValue("first").AsString();
                long pathId = assetIndexField.GetValue("second/asset/m_PathID").AsInt64();
                if (pathId == 0) continue;
                var assetExt = am.GetExtAsset(assetsFileInst, 0, pathId);
                var baseField = assetExt.instance.GetBaseField();

                foreach (var dependency in assetExt.file.GetDependentAssetIds(baseField, am))
                {
                    var preloadEntry = ValueBuilder.DefaultValueFieldFromArrayTemplate(preloadTableArray);
                    var key = (fileId: dependency.FileId, pathId: dependency.PathId);
                    if (remap.ContainsKey(key))
                    {
                        preloadEntry.SetValue("m_FileID", remap[key].fileId);
                        preloadEntry.SetValue("m_PathID", remap[key].pathId);
                    }
                    else
                    {
                        preloadEntry.SetValue("m_FileID", key.fileId);
                        preloadEntry.SetValue("m_PathID", key.pathId);
                    }
                    preloadFieldChildren.Add(preloadEntry);
                }
            }
            if (preloadFieldChildren.Any())
                preloadTableArray.SetChildrenList(preloadFieldChildren.ToArray());
        }

    }
}