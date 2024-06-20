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

                    // STEP 1. Find objects selected by user filters and calculate their dependency lists.
                    // This reults the full set of objects to go in the asset bundle,
                    // but dependent objects have to be resolved separately.
                    Log($"Collecting objects matching filters");
                    List<AssetTree> localFiles;
                    {
                        var compiledFilters = filters.Select(f => (f.assetClass, nameRegex: f.nameRegex.Select(reg => new Regex(reg)).ToArray())).ToArray();
                        var treeEnumeration = sourceFiles
                                .Select(p => am.LoadAssetsFile(p, true))
                                .SelectMany(
                                    af => compiledFilters.SelectMany(
                                        filter => af.CollectAssetTrees(am, filter.nameRegex, filter.assetClass, Log)
                                        )
                                );

                        localFiles = treeEnumeration.SelectMany(tree => tree.WithDeps(true)).Distinct().ToList();
                    }

                    // STEP 2. Allocate bundle fileId slots for everything. Harvest any missing dependency graphs
                    Log(message: $"Generating Tree Map");
                    var localIdMap = new Dictionary<AssetTree, long>();
                    var fileMaps = new HashSet<MapRecord>();
                    for (int i = 0; i < localFiles.Count; i++)
                    {
                        var assetTree = localFiles[i];
                        localIdMap[assetTree] = i + 2;
                        Log("Collecting dependencies", $"Dependencies: {assetTree.name} ({i + 2})", log: true, progress: i / (float)localFiles.Count);
                        // resolve dependency graph for files only included as dependencies.
                        if (assetTree.Children is null)
                        {
                            localFiles[i] = assetTree.sourceData.file.GetHierarchy(am, 0, assetTree.sourceData.info.PathId);
                        }
                    }

                    Log($"Writing Assets");
                    foreach (var localFile in localFiles)
                    {
                        var localFileId = localIdMap[localFile];
                        IContentReplacer replacer;

                        if (localFile.Children.Count > 0)
                        {
                            var remapedBaseField = CreateRemapedContent(Log, localIdMap, fileMaps, localFile);
                            replacer = new DeferredBaseFieldSerializer(remapedBaseField);
                        }
                        else
                        {
                            // If an asset has no outgoing PPtrs and doesn't need to be modified,
                            // lift-and-shift from the source file.
                            var srcFile = localFile.sourceData.file;
                            var srcInfo = localFile.sourceData.info;
                            var dataOffset = srcFile.file.Header.DataOffset;
                            replacer = new ContentReplacerFromStream(
                                srcFile.AssetsStream,
                                dataOffset + srcInfo.ByteOffset,
                                (int)srcInfo.ByteSize
                                );
                            fileMaps.Add(new MapRecord(localIdMap[localFile], (localFile.sourceData.file.name, localFile.PathId)));
                        }

                        int preloadStart = preloadTableArray.Children.Count;
                        int preloadSize = 0;
                        var tableData = localFile.WithDeps(true).Distinct().ToArray();
                        foreach (var data in tableData)
                        {
                            // skip self
                            if (data == localFile)
                            {
                                continue;
                            }
                            var entry = ValueBuilder.DefaultValueFieldFromArrayTemplate(preloadTableArray);
                            entry["m_FileID"].AsInt = 0;
                            entry["m_PathID"].AsLong = localIdMap[data];
                            preloadTableArray.Children.Add(entry);
                            preloadSize++;
                        }

                        if (preloadSize == 0)
                        {
                            preloadStart = 0;
                        }

                        containerArray.CreateEntry(localFile.name, 0, localFileId, preloadStart, preloadSize);

                        // Append this to the intermediate assets file that will be inserted into the bundle
                        // Eventually we might want to use the source AssetTreeData here,
                        // especially for scripts and such that don't exist in the ClassDatabase.
                        var newAssetInfo = AssetFileInfo.Create(
                            bundleAssetsFile,
                            localFileId,
                            localFile.sourceData.info.TypeId,
                            am.ClassDatabase);
                        newAssetInfo.Replacer = replacer;
                        bundleAssetsFile.AssetInfos.Add(newAssetInfo);
                    }

                    var filemapInfo = AddFileMap(am,
                        bundleAssetsFile,
                        containerArray,
                        fileMaps);
                    bundleAssetsFile.AssetInfos.Add(filemapInfo);

                    // The first DirectoryInfo in the bundle is actually an entire assets archive.
                    // Normally this is called something like CAB-XXXXXXX.
                    // So we build an entire assets file with the desired content, and then add it to the actual bundle file.
                    Log("Writing temporary assets file");
                    using (var tempAssetsFile = new FileStream(Path.GetTempFileName(),
                            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None,
                            4096, FileOptions.RandomAccess | FileOptions.DeleteOnClose))
                    using (var tempWriter = new AssetsFileWriter(tempAssetsFile))
                    {
                        bundleAssetsFile.Write(tempWriter);
                        // can't release tempWriter here, because the library will close the stream, which deletes the file.

                        tempAssetsFile.Position = 0;

                        // build the actual asset bundle file
                        Log($"Writing {outputAssetBundlePath}");
                        using var fileStream = File.Open(outputAssetBundlePath, FileMode.Create);
                        using var writer = new AssetsFileWriter(fileStream);
                        var targetBundleFile = AssetsToolsExtensions.CreateEmptyAssetBundle();
                        var dirInfo = AssetBundleDirectoryInfo.Create(bundleName, true);
                        var dirInfoContent = new ContentReplacerFromStream(tempAssetsFile);
                        dirInfo.Replacer = dirInfoContent;
                        targetBundleFile.BlockAndDirInfo.DirectoryInfos.Add(dirInfo);

                        targetBundleFile.Write(writer);
                    }

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
            var baseField = assetTree.sourceData.baseField;

            log(message: $"Remapping ({baseField.TypeName}) {assetTree.name} PPts");
            var distinctChildren = assetTree.Children.Distinct().ToArray();

            var fileMapElements = distinctChildren
                .Select(child => new MapRecord(localIdMap[child], (child.sourceData.file.name, child.PathId)))
                .Prepend(new MapRecord(localIdMap[assetTree], (assetTree.sourceData.file.name, assetTree.PathId)));

            foreach (var map in fileMapElements)
                fileMaps.Add(map);

            var remap = distinctChildren.ToDictionary(child => (child.FileId, child.PathId), child => (0, localIdMap[child]));
            // Some assets can point to themselves.
            remap.Add((0, assetTree.PathId), (0, localIdMap[assetTree]));
            baseField.RemapPPtrs(remap);

            return baseField;
        }

        private static AssetFileInfo AddFileMap(
            AssetsManager am,
            AssetsFile bundleAssetsFile,
            AssetTypeValueField containerArray,
            HashSet<MapRecord> fileMaps)
        {
            const string assetName = "FileMap";
            var templateField = new AssetTypeTemplateField();

            var cldbType = am.ClassDatabase.FindAssetClassByID((int)AssetClassID.TextAsset);
            templateField.FromClassDatabase(am.ClassDatabase, cldbType);

            var textAssetBaseField = ValueBuilder.DefaultValueFieldFromTemplate(templateField);

            var fileMap = new FileMap { Maps = fileMaps.ToArray() };
            var mapJson = EditorJsonUtility.ToJson(fileMap, true);

            textAssetBaseField["m_Name"].AsString = assetName;
            textAssetBaseField["m_Script"].AsString = mapJson;

            var pathId = bundleAssetsFile.AssetInfos.Count + 2;
            AssetFileInfo assetFileInfo = AssetFileInfo.Create(bundleAssetsFile, pathId, (int)AssetClassID.TextAsset, am.ClassDatabase);

            // Use m_Container to construct an blank element for it
            containerArray.CreateEntry($"assets/{assetName}.json".ToLowerInvariant(), 0, pathId);

            assetFileInfo.Replacer = new DeferredBaseFieldSerializer(textAssetBaseField);

            return assetFileInfo;
        }
    }
}
