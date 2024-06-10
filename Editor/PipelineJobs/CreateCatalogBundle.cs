using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using BundleKit.Assets;
using BundleKit.Utility;
using System;
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
            //var assetsReplacers = new List<AssetsReplacer>();

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

                    var targetFiles = Enumerable.Empty<string>().Concat(sharedAssetsFiles).Prepend(resourcesFilePath).Prepend(ggmAssetsPath).ToArray();

                    am.LoadClassPackage(classDataPath);
                    am.LoadClassDatabaseFromPackage(Application.unityVersion);

                    var (bun, bundleAssetsFile, assetBundleExtAsset) = am.LoadBundle(templateBundlePath);

                    var bundleBaseField = assetBundleExtAsset.baseField;

                    var containerArray = bundleBaseField["m_Container.Array"];
                    var dependencyArray = bundleBaseField["m_Dependencies.Array"];
                    var preloadTableArray = bundleBaseField["m_PreloadTable.Array"];

                    var bundleName = Path.GetFileNameWithoutExtension(outputAssetBundlePath);
                    bundleBaseField["m_Name"].AsString = bundleName;
                    bundleBaseField["m_AssetBundleName"].AsString = bundleName;

                    var preloadChildren = new List<AssetTypeValueField>();
                    var mContainerChildren = new List<AssetTypeValueField>();
                    var streamReaders = new Dictionary<string, Stream>();

                    // Is this a good idea?
                    //bundleAssetsFile.file.dependencies.dependencies.Clear();
                    //bundleAssetsFile.file.dependencies.dependencyCount = 0;
                    //bundleAssetsFile.dependencies.Clear();
                    bundleAssetsFile.file.Metadata.Externals.Clear();

                    var compiledFilters = filters.Select(f => (assetClass: f.assetClass, nameRegex: f.nameRegex.Select(reg => new Regex(reg)).ToArray())).ToArray();
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

                        //var assetBytes = asset.instance.WriteToByteArray();

                        //var currentAssetReplacer = new AssetsReplacerFromMemory(0, localId, (int)asset.info.type,
                        //                                                        AssetHelper.GetScriptIndex(asset.file.file, asset.info),
                        //                                                        assetBytes);
                        //assetsReplacers.Add(currentAssetReplacer);
                        mContainerChildren.Add(containerArray.CreateEntry(assetTree.name, 0, localId, preloadIndex, preloadChildren.Count - preloadIndex));
                    }

                    //AddFileMap(am, assetsReplacers, containerArray, preloadTableArray, mContainerChildren, preloadChildren, preloadIndex, fileMaps);

                    preloadTableArray.Children = preloadChildren;
                    containerArray.Children = mContainerChildren;
                    dependencyArray.Children = new();

                    var newAssetBundleBytes = bundleBaseField.WriteToByteArray();
                    //assetsReplacers.Insert(0, new AssetsReplacerFromMemory(0, assetBundleExtAsset.info.index, (int)assetBundleExtAsset.info.curFileType, 0xFFFF, newAssetBundleBytes));

                    foreach (var stream in streamReaders)
                        stream.Value.Dispose();
                    streamReaders.Clear();

                    byte[] newAssetData;
                    using (var bundleStream = new MemoryStream())
                    using (var writer = new AssetsFileWriter(bundleStream))
                    {
                        bundleAssetsFile.file.Write(writer/*, 0, assetsReplacers, 0, am.classFile*/);
                        newAssetData = bundleStream.ToArray();
                    }

                    //var bundles = new List<BundleReplacer>
                    //    {
                    //        new BundleReplacerFromMemory(bundleAssetsFile.name, bundleName, true, newAssetData, -1)
                    //    };
                    using (var fileStream = File.Open(outputAssetBundlePath, FileMode.Create))
                    using (var writer = new AssetsFileWriter(fileStream))
                        bun.file.Write(writer /*, bundles*/);

                    preloadChildren.Clear();
                    mContainerChildren.Clear();
                    //bundles.Clear();
                    localIdMap.Clear();
                    fileMaps.Clear();
                    //bundles = null;

                }
                finally
                {
                    //assetsReplacers.Clear();
                    am.UnloadAll(true);
                }
            return Task.CompletedTask;
        }

        // Obsoleted? AssetsReplacer is gone.
        /*
        private static void AddFileMap(AssetsManager am, List<AssetsReplacer> assetsReplacers, AssetTypeValueField containerArray, AssetTypeValueField preloadTableArray, List<AssetTypeValueField> mContainerChildren, List<AssetTypeValueField> preloadChildren, int preloadIndex, HashSet<MapRecord> fileMaps)
        {
            const string assetName = "FileMap";
            var templateField = new AssetTypeTemplateField();

            var cldbType = AssetHelper.FindAssetClassByID(am.classFile, (int)AssetClassID.TextAsset);
            templateField.FromClassDatabase(am.classFile, cldbType, 0);

            var textAssetBaseField = ValueBuilder.DefaultValueFieldFromTemplate(templateField);

            var fileMap = new FileMap { Maps = fileMaps.ToArray() };
            var mapJson = EditorJsonUtility.ToJson(fileMap, false);

            textAssetBaseField["m_Name"].AsString = assetName;
            textAssetBaseField["m_Script"].AsString = mapJson;

            int pathId = assetsReplacers.Count + 2;
            assetsReplacers.Add(new AssetsReplacerFromMemory(0, pathId, cldbType.classId, 0xffff, textAssetBaseField.WriteToByteArray()));

            var entry = ValueBuilder.DefaultValueFieldFromArrayTemplate(preloadTableArray);
            entry.SetValue("m_FileID", 0);
            entry.SetValue("m_PathID", pathId);
            preloadChildren.Add(entry);

            // Use m_Container to construct an blank element for it
            var pair = containerArray.CreateEntry($"assets/{assetName}.json".ToLowerInvariant(), 0, pathId, preloadIndex, preloadChildren.Count - preloadIndex);
            mContainerChildren.Add(pair);
        }
        */
    }
}
