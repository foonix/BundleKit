using AssetsTools.NET;
using AssetsTools.NET.Extra;
using BundleKit.Assets;
using BundleKit.Assets.Replacers;
using BundleKit.Utility;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                    foreach (var sourceAssetsFile in sourceFiles)
                    {
                        am.LoadAssetsFile(sourceAssetsFile, true);
                    }
                    var resourceManagerDb = new ResourceManagerDb(am);

                    var bundleName = Path.GetFileNameWithoutExtension(outputAssetBundlePath);

                    AssetsToolsExtensions.CreateBundleAssetsFile(bundleName, am.ClassDatabase, out var bundleAssetsFile, out var bundleBaseField);
                    AssetFileInfo cabData = bundleAssetsFile.AssetInfos[0];

                    var containerArray = bundleBaseField["m_Container.Array"];
                    var preloadTableArray = bundleBaseField["m_PreloadTable.Array"];

                    // Find objects selected by user filters
                    // These are our main objects to be show in the catalog browser.
                    Log($"Collecting objects matching filters");
                    foreach (var sourceFile in sourceFiles)
                    {
                        am.LoadAssetsFile(sourceFile, true);
                    }
                    List<AssetTree> localFiles = CollectRootAssets(am, filters, Log, resourceManagerDb).ToList();

                    // Resolve full dependency graphs for root objects and allocate fileId slots.
                    var localIdMap = new Dictionary<AssetTree, long>();
                    var fileMaps = new HashSet<MapRecord>();
                    int rootObjCount = localFiles.Count;
                    for (int i = 0; i < rootObjCount; i++)
                    {
                        var root = localFiles[i];
                        int localId = i + 2;
                        localIdMap[root] = localId;
                        Log("Collecting dependencies", $"Root object: {root.GetBkCatalogName()} ({localId})", log: true, progress: i / (float)localFiles.Count);
                        // resolve dependency graph
                        if (root.Children is null)
                        {
                            localFiles[i] = root.sourceData.file.GetDependencies(
                                am, resourceManagerDb, 0,
                                root.sourceData.info.PathId, true);
                        }
                        fileMaps.Add(new MapRecord(localId, (root.sourceData.file.name, root.PathId)));
                    }

                    // Allocate fileId slots for objects only included as required dependencies.
                    // These don't need names, and their dep graphs are a subset of root objects' dep graphs.
                    var depObjects = localFiles.SelectMany(a => a.Children)
                        .Where(c => !localFiles.Contains(c))  // a root object can depend on another root object.
                        .Distinct().ToList();
                    for (int i = 0; i < depObjects.Count; i++)
                    {
                        var depObj = depObjects[i];
                        int localId = rootObjCount + i + 2;
                        localIdMap[depObj] = localId;
                        Log("Collecting dependencies", $"dep object: {depObj.GetBkCatalogName()} ({localId})", log: true, progress: i / (float)depObjects.Count);
                        if (depObj.Children is null)
                        {
                            // The full dep graph will already be available,
                            // but we do need to know the immediate deps for remapping the PPtrs.
                            depObj = depObj.sourceData.file.GetDependencies(
                                am, resourceManagerDb, 0,
                                depObj.sourceData.info.PathId, false);
                        }
                        localFiles.Add(depObj);
                    }

                    // create CAB entry for root objects only now that we know where deps are going to go.
                    for (int i = 0; i < rootObjCount; i++)
                    {
                        var root = localFiles[i];
                        int localId = i + 2;
                        int preloadStart = preloadTableArray.Children.Count;
                        int preloadSize = 0;
                        var tableData = root.Children.Distinct().ToArray();
                        foreach (var data in tableData)
                        {
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
                        containerArray.CreateEntry(root.GetBkCatalogName(), 0, localId, preloadStart, preloadSize);
                    }

                    Log($"Rewriting Assets");
                    int remapProgressBar = 0;
                    foreach (var localFile in localFiles)
                    {
                        var localFileId = localIdMap[localFile];
                        IContentReplacer replacer;

                        if (localFile.Children.Count > 0)
                        {
                            Log(message: $"Remapping {localFile.GetBkCatalogName()} PPts", progress: remapProgressBar / (float)localFiles.Count);
                            var remapedBaseField = CreateRemapedContent(localIdMap, localFile);
                            replacer = new DeferredBaseFieldSerializer(remapedBaseField);
                        }
                        else
                        {
                            Log(message: $"Direct copying {localFile.GetBkCatalogName()}", progress: remapProgressBar / (float)localFiles.Count);
                            // If an asset has no outgoing PPtrs and doesn't need to be modified,
                            // lift-and-shift from the source file.
                            var srcFile = localFile.sourceData.file;
                            var srcInfo = localFile.sourceData.info;
                            var dataOffset = srcFile.file.Header.DataOffset;
                            replacer = new ContentReplacerFromStream(
                                srcFile.AssetsStream,
                                dataOffset + srcInfo.ByteOffset,
                                (int)srcInfo.ByteSize);
                        }

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

                        remapProgressBar++;
                    }

                    var filemapInfo = AddFileMap(am,
                        bundleAssetsFile,
                        containerArray,
                        fileMaps);
                    bundleAssetsFile.AssetInfos.Add(filemapInfo);

                    // The first DirectoryInfo in the bundle is actually an entire assets archive.
                    // Normally this is called something like CAB-XXXXXXX.
                    // So we build an entire assets file with the desired content, and then add it to the actual bundle file.
                    Log("Writing temporary assets file", progress: 0);
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

        AssetTypeValueField CreateRemapedContent(Dictionary<AssetTree, long> localIdMap, AssetTree assetTree)
        {
            var baseField = assetTree.sourceData.baseField;

            var distinctChildren = assetTree.Children.Distinct().ToArray();

            var fileMapElements = distinctChildren
                .Select(child => new MapRecord(localIdMap[child], (child.sourceData.file.name, child.PathId)))
                .Prepend(new MapRecord(localIdMap[assetTree], (assetTree.sourceData.file.name, assetTree.PathId)));

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
            const string assetName = "BundleKitFileMap";
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
            containerArray.CreateEntry(assetName.ToLowerInvariant(), 0, pathId);

            assetFileInfo.Replacer = new DeferredBaseFieldSerializer(textAssetBaseField);

            return assetFileInfo;
        }

        private static IEnumerable<AssetTree> CollectRootAssets(
            AssetsManager am,
            Filter[] filters,
            UpdateLog Update,
            ResourceManagerDb resourceManagerDb)
        {
            foreach (var assetsFileInst in am.Files)
            {
                Update(message: assetsFileInst.name, log: false);

                foreach (AssetFileInfo assetFileInfo in assetsFileInst.file.AssetInfos)
                {
                    if (!filters.MatchesAnyClass(assetFileInfo))
                    {
                        continue;
                    }

                    var external = am.GetExtAsset(assetsFileInst, 0, assetFileInfo.PathId);
                    resourceManagerDb.TryGetName(assetsFileInst.name, assetFileInfo.PathId, out var rmName);
                    string name = external.GetName(am);


                    if (!(filters.AnyMatch(assetFileInfo, rmName) || filters.AnyMatch(assetFileInfo, name)))
                    {
                        continue;
                    }

                    // we know we want the asset as a root asset at this point

                    bool canHaveDeps = ((AssetClassID)assetFileInfo.TypeId).CanHaveDependencies();

                    // dispose the baseAsset if we're not going to need it for anything other than the name.
                    if (!canHaveDeps)
                    {
                        external.baseField = null;
                    }

                    yield return new AssetTree()
                    {
                        name = name,
                        resourceManagerName = rmName,
                        sourceData = external,
                        FileId = 0,
                        PathId = assetFileInfo.PathId,
                        Children = canHaveDeps
                            ? null    // deps are unknown at this point
                            : new(),  // we know there are no deps
                    };
                }
            }
        }
    }
}
