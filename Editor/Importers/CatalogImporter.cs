using AssetsTools.NET.Extra;
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

            am.PrepareNewBundle(ctx.assetPath, out var bun, out var bundleAssetsFile, out var assetBundleExtAsset);

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
            catch (Exception e)
            {
                Debug.LogError($"Failed to load: {ctx.assetPath}");
            }

            var catalog = ScriptableObject.CreateInstance<Catalog>();
            ctx.AddObjectToAsset("Catalog", catalog);
            ctx.SetMainObject(catalog);

            var assets = new List<Object>();
            var textureLookup = new Dictionary<long, Texture>();
            var allAssets = bundle.LoadAllAssets();
            var assetNames = bundle.GetAllAssetNames();

            for (int i = 0; i < allAssets.Length; i++)
            {
                var asset = allAssets[i];
                assets.Add(asset);
                var foundInfo = AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId);
                if (asset is Shader shader)
                {
                    ShaderUtil.RegisterShader(shader);
                    continue;
                }

                if (foundInfo && asset is Texture2D tex)
                    textureLookup[localId] = tex;

                var identifier = HashingMethods.Calculate<MD4>(localId).ToString();

                ctx.AddObjectToAsset(identifier, asset);
            }

            catalog.objects = assets.ToArray();
        }
    }
}