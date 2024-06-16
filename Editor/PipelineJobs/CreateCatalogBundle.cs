using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using BundleKit.Assets;
using BundleKit.Assets.Replacers;
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
        [Tooltip("File to write to. Path is relative to project directory.")]
        public string outputAssetBundlePath;
        [Tooltip("Object classes to include in bundle")]
        public Filter[] filters;

        private delegate void Log(string title = null, string message = null, float progress = -1, bool log = true, params string[] context);

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

                    var settings = ThunderKitSetting.GetOrCreateSettings<ThunderKitSettings>();
                    var gameName = Path.GetFileNameWithoutExtension(settings.GameExecutable);

                    var dataDirectoryPath = Path.Combine(settings.GamePath, $"{gameName}_Data");
                    var classDataPath = Path.Combine("Packages", "com.passivepicasso.bundlekit", "Library", "classdata.tpk");

                    var sourceFiles = new string[]
                    {
                        Path.Combine(dataDirectoryPath, "globalgamemanagers"),
                        Path.Combine(dataDirectoryPath, "globalgamemanagers.assets"),
                        Path.Combine(dataDirectoryPath, "resources.assets"),
                    }.Concat(Directory.EnumerateFiles(dataDirectoryPath, "sharedassets*.assets"));
                    //var levelFiles = Directory.EnumerateFiles(dataDirectoryPath, "level*").Where(file => Path.GetExtension(file) == string.Empty).ToArray();

                    am.LoadClassPackage(classDataPath);
                    am.LoadClassDatabaseFromPackage(Application.unityVersion);

                    var bundleName = Path.GetFileNameWithoutExtension(outputAssetBundlePath);

                    AssetsToolsExtensions.CreateBundleAssetsFile(bundleName, am.ClassDatabase, out var bundleAssetsFile, out var bundleBaseField);
                    AssetFileInfo cabData = bundleAssetsFile.AssetInfos[0];

                    var containerArray = bundleBaseField["m_Container.Array"];
                    var preloadTableArray = bundleBaseField["m_PreloadTable.Array"];

                    var preloadChildren = new List<AssetTypeValueField>();
                    var mContainerChildren = new List<AssetTypeValueField>();

                    IGrouping<AssetTree, AssetTree>[] localGroups;
                    {
                        var compiledFilters = filters.Select(f => (f.assetClass, nameRegex: f.nameRegex.Select(reg => new Regex(reg)).ToArray())).ToArray();
                        var treeEnumeration = sourceFiles
                                .Select(p => am.LoadAssetsFile(p, false))
                                .SelectMany(af => compiledFilters.SelectMany(filter => af.CollectAssetTrees(am, filter.nameRegex, filter.assetClass, Log)));

                        var felledTree = treeEnumeration.SelectMany(tree => tree.Flatten(true));
                        localGroups = felledTree.GroupBy(tree => tree).ToArray();
                    }
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
                        preloadIndex = preloadChildren.Count;
                        IContentReplacer replacer;

                        if (assetTree.Children.Count > 0)
                        {
                            var remapedBaseField = CreateRemapedContent(Log, localIdMap, fileMaps, assetTree);
                            replacer = new DeferredBaseFieldSerializer(remapedBaseField);
                        }
                        else
                        {
                            // If an asset has no outgoing PPtrs and doesn't need to be modified,
                            // lift-and-shift from the source file.
                            var srcFile = assetTree.assetExternal.file;
                            var srcInfo = assetTree.assetExternal.info;
                            var dataOffset = srcFile.file.Header.DataOffset;
                            replacer = new ContentReplacerFromStream(
                                srcFile.AssetsStream,
                                dataOffset + srcInfo.ByteOffset,
                                (int)srcInfo.ByteSize
                                );
                        }

                        var tableData = assetTree.Flatten(true).Distinct().ToArray();
                        foreach (var data in tableData)
                        {
                            var entry = ValueBuilder.DefaultValueFieldFromArrayTemplate(preloadTableArray);
                            entry["m_FileID"].AsInt = 0;
                            entry["m_PathID"].AsLong = localIdMap[data];
                            preloadChildren.Add(entry);
                        }

                        mContainerChildren.Add(containerArray.CreateEntry(assetTree.name, 0, localId, preloadIndex, preloadChildren.Count - preloadIndex));

                        // Append this to the intermediate assets file that will be inserted into the bundle
                        // Eventually we might want to use the source AssetTreeData here,
                        // especially for scripts and such that don't exist in the ClassDatabase.
                        var newAssetInfo = AssetFileInfo.Create(
                            bundleAssetsFile,
                            localId,
                            assetTree.assetExternal.info.TypeId,
                            am.ClassDatabase);
                        newAssetInfo.Replacer = replacer;
                        bundleAssetsFile.AssetInfos.Add(newAssetInfo);
                    }

                    var filemapInfo = AddFileMap(am,
                        bundleAssetsFile,
                        containerArray,
                        preloadTableArray,
                        mContainerChildren,
                        preloadChildren,
                        preloadIndex,
                        fileMaps);
                    bundleAssetsFile.AssetInfos.Add(filemapInfo);

                    preloadTableArray.Children = preloadChildren;
                    containerArray.Children = mContainerChildren;

                    // The first DirectoryInfo in the bundle is actually an entire assets archive.
                    // Normally this is called something like CAB-XXXXXXX.
                    // So we build an entire assets file with the desired content, and then add it to the bundle.
                    byte[] newAssetData;
                    using (var bundleStream = new MemoryStream())
                    using (var writer = new AssetsFileWriter(bundleStream))
                    {
                        bundleAssetsFile.Write(writer);
                        newAssetData = bundleStream.ToArray();
                    }

                    // build the actual asset bundle file
                    using (var fileStream = File.Open(outputAssetBundlePath, FileMode.Create))
                    using (var writer = new AssetsFileWriter(fileStream))
                    {
                        var targetBundleFile = AssetsToolsExtensions.CreateEmptyAssetBundle();
                        var dirInfo = AssetBundleDirectoryInfo.Create(bundleName, true);
                        var dirInfoContent = new ContentReplacerFromBuffer(newAssetData);
                        dirInfo.Replacer = dirInfoContent;
                        targetBundleFile.BlockAndDirInfo.DirectoryInfos.Add(dirInfo);

                        targetBundleFile.Write(writer);
                    }

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

        AssetTypeValueField CreateRemapedContent(Log log, Dictionary<AssetTree, long> localIdMap, HashSet<MapRecord> fileMaps, AssetTree assetTree)
        {
            var baseField = assetTree.assetExternal.baseField;

            log(message: $"Remapping ({baseField.TypeName}) {assetTree.name} PPts");
            var distinctChildren = assetTree.Children.Distinct().ToArray();

            var fileMapElements = distinctChildren
                .Select(child => new MapRecord(localIdMap[child], (child.assetExternal.file.name, child.PathId)))
                .Prepend(new MapRecord(localIdMap[assetTree], (assetTree.assetExternal.file.name, assetTree.PathId)));

            foreach (var map in fileMapElements)
                fileMaps.Add(map);

            var remap = distinctChildren.ToDictionary(child => (child.FileId, child.PathId), child => (0, localIdMap[child]));
            baseField.RemapPPtrs(remap);
            switch (baseField.TypeName)
            {
                case "Texture2D":
                case "Cubemap":
                    TextureFile texFile = TextureFile.ReadTextureFile(baseField);
                    texFile.WriteTo(baseField);
                    break;
            }

            return baseField;
        }

        private static AssetFileInfo AddFileMap(
            AssetsManager am,
            AssetsFile bundleAssetsFile,
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

            var pathId = bundleAssetsFile.AssetInfos.Count + 2;
            AssetFileInfo assetFileInfo = AssetFileInfo.Create(bundleAssetsFile, pathId, (int)AssetClassID.TextAsset, am.ClassDatabase);

            var entry = ValueBuilder.DefaultValueFieldFromArrayTemplate(preloadTableArray);
            entry["m_FileID"].AsInt = 0;
            entry["m_PathID"].AsInt = pathId;
            preloadChildren.Add(entry);

            // Use m_Container to construct an blank element for it
            var pair = containerArray.CreateEntry($"assets/{assetName}.json".ToLowerInvariant(), 0, pathId, preloadIndex, preloadChildren.Count - preloadIndex);
            mContainerChildren.Add(pair);

            assetFileInfo.Replacer = new DeferredBaseFieldSerializer(textAssetBaseField);

            return assetFileInfo;
        }
    }
}
