using AssetsTools.NET.Extra;
using BundleKit.Assets;
using BundleKit.PipelineJobs;
using BundleKit.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEditor.Experimental.AssetImporters;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BundleKit.Bundles
{
    [ScriptedImporter(3, new[] { Extension })]
    public class CatalogImporter : ScriptedImporter
    {
        public const string Extension = "catalog";
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var am = new AssetsManager();
            var classDataPath = Path.Combine("Packages", "com.passivepicasso.bundlekit", "Library", "classdata.tpk");
            am.LoadClassPackage(classDataPath);
            am.LoadClassDatabaseFromPackage(Application.unityVersion);

            var (bun, bundleAssetsFile, assetBundleExtAsset) = am.LoadBundle(ctx.assetPath);

            var bundleBaseField = assetBundleExtAsset.instance.GetBaseField();
            var dependencyArray = bundleBaseField.GetField("m_Dependencies/Array");
            var dependencies = dependencyArray.GetChildrenList().Select(dep => dep.GetValue().AsString()).ToArray();
            var container = bundleBaseField.GetField("m_Container/Array");
            var bundleName = bundleBaseField.GetValue("m_AssetBundleName").AsString();

            am.UnloadAll();

            var localPath = Path.GetDirectoryName(ctx.assetPath);

            var loadedBundles = AssetBundle.GetAllLoadedAssetBundles();
            var bundle = loadedBundles.FirstOrDefault(bnd => bundleName.Equals(bnd.name));
            bundle?.Unload(true);
            try
            {
                bundle = AssetBundle.LoadFromFile(ctx.assetPath);
            }
            catch (Exception)
            {
                Debug.LogError($"Failed to load: {ctx.assetPath}");
            }

            var allAssets = bundle.LoadAllAssets();
            var allNames = bundle.GetAllAssetNames();

            var catalog = ScriptableObject.CreateInstance<Catalog>();
            ctx.AddObjectToAsset("Catalog", catalog);
            ctx.SetMainObject(catalog);
            catalog.Assets = new List<AssetMap>();

            for (int i = 0; i < allAssets.Length; i++)
            {
                var asset = allAssets[i];
                if (asset.name == "FileMap") continue;
                if (asset is Shader shader)
                {
                    ShaderUtil.RegisterShader(shader);
                    continue;
                }

                var foundInfo = AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId);
                ctx.AddObjectToAsset($"{localId}", asset);
            }
        }
    }
}