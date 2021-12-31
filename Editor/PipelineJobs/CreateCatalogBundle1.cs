using AssetsTools.NET;
using AssetsTools.NET.Extra;
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
    [PipelineSupport(typeof(Pipeline))]
    public class CreateCatalogBundle1 : PipelineJob
    {
        public DefaultAsset templateBundle;
        public string outputAssetBundlePath;
        public Filter[] filters;

        public override Task Execute(Pipeline pipeline)
        {
            var am = new AssetsManager();
            var assetsReplacers = new List<AssetsReplacer>();
            var bundleReplacers = new List<BundleReplacer>();

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
                    am.PrepareNewBundle(templateBundlePath, out var bun, out var bundleAssetsFile, out var assetBundleExtAsset);

                    var bundleBaseField = assetBundleExtAsset.instance.GetBaseField();

                    var containerArray = bundleBaseField.GetField("m_Container/Array");
                    var dependencyArray = bundleBaseField.GetField("m_Dependencies/Array");
                    var preloadTableArray = bundleBaseField.GetField("m_PreloadTable/Array");

                    var dependencyChildren = new List<AssetTypeValueField>();
                    var mContainerChildren = new List<AssetTypeValueField>();
                    var dependencies = new Dictionary<string, int>();

                    bundleAssetsFile.file.dependencies.dependencies.Clear();

                    var compiledFilters = filters.Select(f => (assetClass: f.assetClass, nameRegex: f.nameRegex.Select(reg => new Regex(reg)).ToArray())).ToArray();
                    IEnumerable<AssetTree> treeEnumeration =
                        targetFiles.SelectMany(p =>
                            compiledFilters.SelectMany(filter =>
                                am.GetAssetsInst(p).CollectAssetTrees(am, filter.nameRegex, filter.assetClass, Log)
                            )
                        );

                    foreach (var tree in treeEnumeration)
                    {
                        if (!dependencies.ContainsKey(tree.assetExternal.file.name))
                            dependencies[tree.assetExternal.file.name] = dependencies.Count + 1;

                        mContainerChildren.Add(containerArray.CreateEntry(tree.name, dependencies[tree.assetExternal.file.name], tree.PathId));
                    }

                    foreach (var dependency in dependencies.OrderBy(dep => dep.Value).Select(dep => dep.Key))
                    {
                        string path = Path.Combine(dataDirectoryPath, dependency);
                        bundleAssetsFile.AddDependency(path);

                        var depTemplate = ValueBuilder.DefaultValueFieldFromArrayTemplate(dependencyArray);
                        depTemplate.GetValue().Set(path);
                        dependencyChildren.Add(depTemplate);
                    }

                    preloadTableArray.SetChildrenList(Array.Empty<AssetTypeValueField>());
                    containerArray.SetChildrenList(mContainerChildren.ToArray());
                    dependencyArray.SetChildrenList(dependencyChildren.ToArray());

                    var newAssetBundleBytes = bundleBaseField.WriteToByteArray();
                    assetsReplacers.Add(new AssetsReplacerFromMemory(0, assetBundleExtAsset.info.index, (int)assetBundleExtAsset.info.curFileType, 0xFFFF, newAssetBundleBytes));

                    byte[] newAssetData;
                    using (var bundleStream = new MemoryStream())
                    using (var writer = new AssetsFileWriter(bundleStream))
                    {
                        bundleAssetsFile.file.Write(writer, 0, assetsReplacers, 0);
                        newAssetData = bundleStream.ToArray();
                    }
                    var bundleReplacer = new BundleReplacerFromMemory(bundleAssetsFile.name, bundleAssetsFile.name, true, newAssetData, -1);
                    bundleReplacers.Add(bundleReplacer);

                    using (var file = File.OpenWrite(outputAssetBundlePath))
                    using (var writer = new AssetsFileWriter(file))
                        bun.file.Write(writer, bundleReplacers);
                }
                finally
                {
                    am.UnloadAll(true);
                }
            return Task.CompletedTask;
        }

    }
}
