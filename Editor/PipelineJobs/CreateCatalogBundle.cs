using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using BundleKit.Assets;
using BundleKit.Utility;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ThunderKit.Common.Logging;
using ThunderKit.Core.Data;
using ThunderKit.Core.Pipelines;
using UnityEditor;
using UnityEngine;
using static BundleKit.Utility.Extensions;

namespace BundleKit.PipelineJobs
{
    /// <summary>
    /// Creates an AssetBundle from assets in an existing Unity build.
    /// Set the build to read from in the ThunderKit Game Project settings.
    /// </summary>
    [PipelineSupport(typeof(Pipeline))]
    public class CreateCatalogBundle : PipelineJob
    {
        [Tooltip("A bundle used to initialize the output bundle.")]
        public DefaultAsset templateBundle;
        [Tooltip("File to write to. Path is relative to project directory.")]
        public string outputAssetBundlePath;
        [Tooltip("Object classes to include in bundle")]
        public Filter[] filters;

        public override Task Execute(Pipeline pipeline)
        {
            var am = new AssetsManager();

            using (var progressBar = new ProgressBar("Constructing AssetBundle"))
                try
                {
                    void Log(string title = null, string message = null, float progress = -1, bool log = true, params string[] context)
                    {
                        if (log && (message ?? title) != null)
                            pipeline.Log(LogLevel.Information, message ?? title, context);

                        progressBar.Update(message, title, progress);
                    }

                    var templateBundlePath = AssetDatabase.GetAssetPath(templateBundle);

                    var settings = ThunderKitSetting.GetOrCreateSettings<ThunderKitSettings>();
                    var gameName = Path.GetFileNameWithoutExtension(settings.GameExecutable);

                    var dataDirectoryPath = Path.Combine(settings.GamePath, $"{gameName}_Data");
                    var classDataPath = Path.Combine("Packages", "com.passivepicasso.bundlekit", "Library", "classdata.tpk");

                    var sharedAssetsFiles = Directory.EnumerateFiles(dataDirectoryPath, "sharedassets*.assets").ToArray();
                    var levelFiles = Directory.EnumerateFiles(dataDirectoryPath, "level*").Where(file => Path.GetExtension(file) == string.Empty).ToArray();
                    var resourcesFilePath = Path.Combine(dataDirectoryPath, "resources.assets");
                    var ggmAssetsPath = Path.Combine(dataDirectoryPath, "globalgamemanagers.assets");
                    var ggmPath = Path.Combine(dataDirectoryPath, "globalgamemanagers");

                    var targetFiles = new string[]
                    {
                        ggmPath,
                        ggmAssetsPath,
                        resourcesFilePath,
                    }.Concat(sharedAssetsFiles);

                    am.LoadClassPackage(classDataPath);
                    am.LoadClassDatabaseFromPackage(Application.unityVersion);

                    var (bun, bundleAssetsFile, assetBundleExtAsset) = am.LoadBundle(templateBundlePath);

                    var bundleBaseField = assetBundleExtAsset.baseField;

                    var containerArray = bundleBaseField["m_Container.Array"];
                    var preloadTableArray = bundleBaseField["m_PreloadTable.Array"];

                    var bundleName = Path.GetFileNameWithoutExtension(outputAssetBundlePath);
                    bundleBaseField["m_Name"].AsString = bundleName;
                    bundleBaseField["m_AssetBundleName"].AsString = bundleName;

                    var preloadChildren = new List<AssetTypeValueField>();
                    var mContainerChildren = new List<AssetTypeValueField>();

                    bundleAssetsFile.file.Metadata.Externals.Clear();

                    var compiledFilters = filters.Select(f => (f.assetClass, nameRegex: f.nameRegex.Select(reg => new Regex(reg)).ToArray())).ToArray();
                    var treeEnumeration = targetFiles
                            .Select(p => am.LoadAssetsFile(p, false))
                            .SelectMany(af => compiledFilters.SelectMany(filter => af.CollectAssetTrees(am, filter.nameRegex, filter.assetClass, Log)));

                    var felledTree = treeEnumeration.SelectMany(tree => tree.Flatten(true));
                    var localGroups = felledTree.GroupBy(tree => tree).ToArray();
                    var localIdMap = new Dictionary<AssetTree, long>();
                    var fileMaps = new HashSet<MapRecord>();
                    var preloadIndex = 0;

                    Log($"Generating Tree Map");
                    for (long i = 0; i < localGroups.Length; i++)
                    {
                        var assetTree = localGroups[i].First();
                        localIdMap[assetTree] = i + 2;
                        Log(message: $"{assetTree.name} = {i + 2}");
                    }
                    Log($"Writing Assets");
                    foreach (var group in localGroups)
                    {
                        var assetTree = group.First();
                        var localId = localIdMap[group.Key];
                        var asset = assetTree.assetExternal;
                        var baseField = assetTree.assetExternal.baseField;

                        Log(message: $"Remapping ({baseField.TypeName}) {assetTree.name} PPts");
                        var distinctChildren = assetTree.Children.Distinct().ToArray();

                        var fileMapElements = distinctChildren
                            .Select(child => new MapRecord(localIdMap[child], (child.assetExternal.file.name, child.PathId)))
                            .Prepend(new MapRecord(localIdMap[assetTree], (assetTree.assetExternal.file.name, assetTree.PathId)));

                        foreach (var map in fileMapElements)
                            fileMaps.Add(map);

                        var remap = distinctChildren.ToDictionary(child => (child.FileId, child.PathId), child => (0, localIdMap[child]));
                        baseField.RemapPPtrs(remap);

                        var tableData = assetTree.Flatten(true).Distinct().ToArray();
                        preloadIndex = preloadChildren.Count;
                        foreach (var data in tableData)
                        {
                            var entry = ValueBuilder.DefaultValueFieldFromArrayTemplate(preloadTableArray);
                            entry["m_FileID"].AsInt = 0;
                            entry["m_PathID"].AsLong = localIdMap[data];
                            preloadChildren.Add(entry);
                        }
                        switch (baseField.TypeName)
                        {
                            case "Texture2D":
                            case "Cubemap":
                                TextureFile texFile = TextureFile.ReadTextureFile(baseField);
                                texFile.WriteTo(baseField);
                                break;
                        }

                        var assetBytes = asset.baseField.WriteToByteArray();

                        mContainerChildren.Add(containerArray.CreateEntry(assetTree.name, 0, localId, preloadIndex, preloadChildren.Count - preloadIndex));

                        // append this to the intermediate assets file that will be inserted into the bundle
                        var currentAssetInfo = AssetFileInfo.Create(bundleAssetsFile.file, localId, asset.info.TypeId, am.ClassDatabase);
                        currentAssetInfo.Replacer = new ContentReplacerFromBuffer(assetBytes);
                        bundleAssetsFile.file.AssetInfos.Add(currentAssetInfo);
                    }

                    var filemapInfo = AddFileMap(am,
                        bundleAssetsFile,
                        containerArray,
                        preloadTableArray,
                        mContainerChildren,
                        preloadChildren,
                        preloadIndex,
                        fileMaps);
                    bundleAssetsFile.file.AssetInfos.Add(filemapInfo);

                    preloadTableArray.Children = preloadChildren;
                    containerArray.Children = mContainerChildren;

                    var newAssetBundleBytes = bundleBaseField.WriteToByteArray();
                    var toReplace = assetBundleExtAsset.file.file.GetAssetInfo(assetBundleExtAsset.info.PathId);
                    var replacer = new ContentReplacerFromBuffer(newAssetBundleBytes);
                    toReplace.Replacer = replacer;

                    // The first DirectoryInfo in the bundle is actually an entire assets archive.
                    // Normally this is called something like CAB-XXXXXXX.
                    // So we build an entire assets file with the desired content, and then add it to the bundle.
                    byte[] newAssetData;
                    using (var bundleStream = new MemoryStream())
                    using (var writer = new AssetsFileWriter(bundleStream))
                    {
                        bundleAssetsFile.file.Write(writer);
                        newAssetData = bundleStream.ToArray();
                    }

                    var cab = bun.file.BlockAndDirInfo.DirectoryInfos[0];
                    var cabReplacer = new ContentReplacerFromBuffer(newAssetData);
                    cab.Name = bundleName;
                    cab.Replacer = cabReplacer;

                    using (var fileStream = File.Open(outputAssetBundlePath, FileMode.Create))
                    using (var writer = new AssetsFileWriter(fileStream))
                        bun.file.Write(writer);

                    preloadChildren.Clear();
                    mContainerChildren.Clear();
                    localIdMap.Clear();
                    fileMaps.Clear();
                }
                finally
                {
                    am.UnloadAll(true);
                }
            return Task.CompletedTask;
        }

        private static AssetFileInfo AddFileMap(
            AssetsManager am,
            AssetsFileInstance bundleInstance,
            AssetTypeValueField containerArray,
            AssetTypeValueField preloadTableArray,
            List<AssetTypeValueField> mContainerChildren,
            List<AssetTypeValueField> preloadChildren,
            int preloadIndex,
            HashSet<MapRecord> fileMaps)
        {
            const string assetName = "FileMap";
            var templateField = new AssetTypeTemplateField();

            var cldbType = am.ClassDatabase.FindAssetClassByID((int)AssetClassID.TextAsset);
            templateField.FromClassDatabase(am.ClassDatabase, cldbType);

            var textAssetBaseField = ValueBuilder.DefaultValueFieldFromTemplate(templateField);

            var fileMap = new FileMap { Maps = fileMaps.ToArray() };
            var mapJson = EditorJsonUtility.ToJson(fileMap, false);

            textAssetBaseField["m_Name"].AsString = assetName;
            textAssetBaseField["m_Script"].AsString = mapJson;

            var pathId = bundleInstance.file.AssetInfos.Count;
            AssetFileInfo assetFileInfo = AssetFileInfo.Create(bundleInstance.file, pathId, (int)AssetClassID.TextAsset, am.ClassDatabase);

            var entry = ValueBuilder.DefaultValueFieldFromArrayTemplate(preloadTableArray);
            entry["m_FileID"].AsInt = 0;
            entry["m_PathID"].AsInt = pathId;
            preloadChildren.Add(entry);

            // Use m_Container to construct an blank element for it
            var pair = containerArray.CreateEntry($"assets/{assetName}.json".ToLowerInvariant(), 0, pathId, preloadIndex, preloadChildren.Count - preloadIndex);
            mContainerChildren.Add(pair);

            var replacer = new ContentReplacerFromBuffer(textAssetBaseField.WriteToByteArray());
            assetFileInfo.Replacer = replacer;

            return assetFileInfo;
        }
    }
}
