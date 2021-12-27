using AssetsTools.NET;
using AssetsTools.NET.Extra;
using BundleKit.Bundles;
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

namespace BundleKit.PipelineJobs
{
    using static AssetToolPipelineMethods;

    public delegate void UpdateLog(string title = null, string message = null, float progress = -1, bool log = true, params string[] context);

    [PipelineSupport(typeof(Pipeline))]
    public class CreateResourceBundle : PipelineJob
    {
        const string unityBuiltinExtra = "Resources/unity_builtin_extra";
        const string unityDefaultResources = "Resources/unity default resources";

        [Serializable]
        public struct Filter
        {
            public string[] nameRegex;
            public AssetClassID assetClass;
        }

        public DefaultAsset bundle;
        public string outputDirectory;
        public Filter[] filters;

        AssetsManager am;

        struct LoadSet
        {
            public readonly string[] AssetBundlePaths;
            public LoadSet(string[] assetBundlePaths) => AssetBundlePaths = assetBundlePaths;
        }


        public override Task Execute(Pipeline pipeline)
        {
            pipeline.Log(LogLevel.Information, "Constructing AssetBundle");
            am = new AssetsManager();
            using (var progressBar = new ProgressBar("Constructing AssetBundle"))
                try
                {
                    void Log(string title = null, string message = null, float progress = -1, bool log = true, params string[] context)
                    {
                        if (log && (message ?? title) != null)
                            pipeline.Log(LogLevel.Information, message ?? title, context);

                        progressBar.Update(message, title, progress);
                    }

                    if (!AssetDatabase.IsValidFolder(outputDirectory))
                        return Task.CompletedTask;

                    var settings = ThunderKitSetting.GetOrCreateSettings<ThunderKitSettings>();
                    string gameName = Path.GetFileNameWithoutExtension(settings.GameExecutable);
                    var dataDirectoryPath = Path.Combine(settings.GamePath, $"{gameName}_Data");

                    var sharedAssetsFiles = Directory.EnumerateFiles(dataDirectoryPath, "sharedassets*.assets").ToArray();
                    var resourcesFilePath = Path.Combine(dataDirectoryPath, "resources.assets");
                    var ggmPath = Path.Combine(dataDirectoryPath, "globalgamemanagers.assets");

                    var targetFiles = sharedAssetsFiles.Prepend(resourcesFilePath).Prepend(ggmPath).ToArray();

                    var templateBundlePath = AssetDatabase.GetAssetPath(bundle);

                    var contexts = new List<string>();
                    var assetsReplacers = new List<AssetsReplacer>();
                    var newContainerChildren = new List<AssetTypeValueField>();

                    //load data for classes
                    var classDataPath = Path.Combine("Packages", "com.passivepicasso.bundlekit", "Library", "classdata.tpk");
                    am.LoadClassPackage(classDataPath);
                    am.LoadClassDatabaseFromPackage(Application.unityVersion);

                    var visited = new HashSet<AssetID>();

                    IEnumerable<Assets.AssetData> collected = targetFiles.SelectMany(p =>
                            filters.SelectMany(filter =>
                            {
                                var nameRegex = filter.nameRegex.Select(reg => new Regex(reg)).ToArray();
                                return CollectAssets(am, am.GetAssetsInst(p), visited, nameRegex, filter.assetClass, Log);
                            })
                        );

                    var assetsByFile = collected.GroupBy(data => data.AssetFileName).ToArray();
                    var streamReaders = new Dictionary<string, Stream>();
                    var bundles = new HashSet<BundleFileInstance>();
                    foreach (var assetGroup in assetsByFile)
                    {
                        newContainerChildren.Clear();
                        assetsReplacers.Clear();
                        var groupArray = assetGroup.ToArray();
                        var dupeGroups = groupArray.GroupBy(data => data.PathId).ToArray();
                        var assets = dupeGroups.Select(group => group.First()).ToList();
                        string fileName = $"{assetGroup.Key}reference";
                        var assetsFileInst = assets.First().AssetExt.file;
                        var outputPath = Path.Combine(outputDirectory, fileName);

                        Log($"Constructing Bundle: {fileName}");
                        am.PrepareNewBundle(templateBundlePath, out var bun, out var bundleAssetsFile, out var assetBundleExtAsset);

                        // Update bundle assets name and bundle name to the name specified in outputAssetBundlePath
                        var bundleBaseField = assetBundleExtAsset.instance.GetBaseField();
                        bundleBaseField.SetValue("m_Name", fileName);
                        bundleBaseField.SetValue("m_AssetBundleName", fileName);
                        //bundleAssetsFile.file.typeTree = assetsFileInst.file.typeTree;

                        bundleAssetsFile.file.dependencies.dependencies.Clear();
                        foreach (var dependency in assetsFileInst.file.dependencies.dependencies)
                            bundleAssetsFile.AddDependency(dependency);

                        UpdateAssetBundleDependencies(bundleBaseField, bundleAssetsFile.file.dependencies.dependencies);

                        Log($"Updating {fileName} Container Array");
                        // Get container for populating asset listings
                        var containerArray = bundleBaseField.GetField("m_Container/Array");
                        foreach (var (asset, assetName, assetFileName, fileId, pathId, depth) in assets)
                        {
                            var baseField = asset.instance.GetBaseField();
                            Log($"Import {assetName} ({baseField.GetFieldType()})", "Import");

                            var streamDatas = baseField.FindField("m_StreamData").ToArray();
                            if (streamDatas.Any())
                            {
                                Log(message: $"Import {assetName} ({baseField.GetFieldType()}) Stream Data");
                                foreach (var streamData in streamDatas)
                                    if (streamData?.children != null)
                                    {

                                        var path = streamData.Get("path");
                                        var streamPath = path.GetValue().AsString();
                                        var newPath = Path.Combine(dataDirectoryPath, streamPath);
                                        var fixedNewPath = newPath.Replace("\\", "/");
                                        var m_Width = baseField.Get("m_Width").GetValue().AsInt();
                                        var m_Height = baseField.Get("m_Height").GetValue().AsInt();
                                        var m_TextureFormat = (AssetsTools.NET.TextureFormat)baseField.Get("m_TextureFormat").GetValue().AsInt();

                                        var offset = streamData.GetValue("offset").AsInt64();
                                        var size = streamData.GetValue("size").AsInt();
                                        var data = new byte[size];
                                        try
                                        {
                                            Stream stream;
                                            if (streamReaders.ContainsKey(fixedNewPath))
                                                stream = streamReaders[fixedNewPath];
                                            else
                                                streamReaders[fixedNewPath] = stream = File.OpenRead(fixedNewPath);

                                            stream.Position = offset;
                                            stream.Read(data, 0, (int)size);
                                            if (data != null && data.Length > 0)
                                            {
                                                streamData.SetValue("offset", 0);
                                                streamData.SetValue("size", 0);
                                                streamData.SetValue("path", string.Empty);
                                                var image_data = baseField.GetField("image data");
                                                image_data.GetValue().type = EnumValueTypes.ByteArray;
                                                image_data.templateField.valueType = EnumValueTypes.ByteArray;
                                                var byteArray = new AssetTypeByteArray()
                                                {
                                                    size = (uint)data.Length,
                                                    data = data
                                                };
                                                image_data.GetValue().Set(byteArray);
                                                baseField.Get("m_CompleteImageSize").GetValue().Set(data.Length);
                                            }
                                            else
                                            {
                                                streamData.SetValue("path", fixedNewPath);
                                            }
                                        }
                                        catch (Exception e)
                                        {
                                            UnityEngine.Debug.LogError(e);
                                        }
                                    }

                            }
                            var otherBytes = asset.instance.WriteToByteArray();
                            var currentAssetReplacer = new AssetsReplacerFromMemory(0, pathId, (int)asset.info.curFileType,
                                                                                    AssetHelper.GetScriptIndex(asset.file.file, asset.info),
                                                                                    otherBytes);
                            assetsReplacers.Add(currentAssetReplacer);

                            // Create entry in m_Container to make this asset visible in the API, otherwise said the asset can be found with AssetBundles.LoadAsset* methods
                            newContainerChildren.Add(containerArray.CreateEntry(assetName, fileId, pathId));
                        }

                        containerArray.SetChildrenList(newContainerChildren.ToArray());

                        bundleBaseField.GetField("m_PreloadTable/Array").SetChildrenList(Array.Empty<AssetTypeValueField>());

                        Log($"Writing {fileName} AssetBundle Asset field");
                        //Save changes for building new bundle file
                        var newAssetBundleBytes = bundleBaseField.WriteToByteArray();
                        assetsReplacers.Add(new AssetsReplacerFromMemory(0, assetBundleExtAsset.info.index, (int)assetBundleExtAsset.info.curFileType, 0xFFFF, newAssetBundleBytes));
                        assetsReplacers = assetsReplacers.GroupBy(repl => repl.GetPathID()).Select(g => g.First()).OrderBy(repl => repl.GetPathID()).ToList();

                        if (File.Exists(outputPath)) File.Delete(outputPath);

                        Log($"Writing {fileName}");
                        byte[] newAssetData;
                        using (var bundleStream = new MemoryStream())
                        using (var writer = new AssetsFileWriter(bundleStream))
                        {
                            //We need to order the replacers by their pathId for Unity to be able to read the Bundle correctly.
                            bundleAssetsFile.file.Write(writer, 0, assetsReplacers, 0, am.classFile);
                            newAssetData = bundleStream.ToArray();
                        }
                        foreach (var replacer in assetsReplacers)
                            replacer.Dispose();

                        using (var file = File.OpenWrite(outputPath))
                        using (var writer = new AssetsFileWriter(file))
                            bun.file.Write(writer, new List<BundleReplacer>
                            {
                                new BundleReplacerFromMemory(bundleAssetsFile.name, fileName, true, newAssetData, newAssetData.Length),
                            });
                    }

                    foreach (var stream in streamReaders)
                        stream.Value.Dispose();

                    Log($"Finished Building Bundle", context: contexts.ToArray());
                }
                finally
                {
                    am.UnloadAll(true);
                }

            AssetDatabase.Refresh();
            return Task.CompletedTask;
        }

    }

    internal struct DependencyDatum
    {
        public AssetsFileInstance file;
        public List<AssetsFileDependency> dependencies;
        public int dependencyCount;

        public DependencyDatum(AssetsFileInstance file, List<AssetsFileDependency> dependencies, int dependencyCount)
        {
            this.file = file;
            this.dependencies = dependencies;
            this.dependencyCount = dependencyCount;
        }

        public override bool Equals(object obj)
        {
            return obj is DependencyDatum other &&
                   EqualityComparer<AssetsFileInstance>.Default.Equals(file, other.file) &&
                   EqualityComparer<List<AssetsFileDependency>>.Default.Equals(dependencies, other.dependencies) &&
                   dependencyCount == other.dependencyCount;
        }

        public override int GetHashCode()
        {
            int hashCode = -1338859138;
            hashCode = hashCode * -1521134295 + EqualityComparer<AssetsFileInstance>.Default.GetHashCode(file);
            hashCode = hashCode * -1521134295 + EqualityComparer<List<AssetsFileDependency>>.Default.GetHashCode(dependencies);
            hashCode = hashCode * -1521134295 + dependencyCount.GetHashCode();
            return hashCode;
        }

        public void Deconstruct(out AssetsFileInstance file, out List<AssetsFileDependency> dependencies, out int dependencyCount)
        {
            file = this.file;
            dependencies = this.dependencies;
            dependencyCount = this.dependencyCount;
        }

        public static implicit operator (AssetsFileInstance file, List<AssetsFileDependency> dependencies, int dependencyCount)(DependencyDatum value)
        {
            return (value.file, value.dependencies, value.dependencyCount);
        }

        public static implicit operator DependencyDatum((AssetsFileInstance file, List<AssetsFileDependency> dependencies, int dependencyCount) value)
        {
            return new DependencyDatum(value.file, value.dependencies, value.dependencyCount);
        }
    }
}