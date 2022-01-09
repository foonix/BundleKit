using AssetsTools.NET.Extra;
using BundleKit.Assets;
using BundleKit.Utility;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BundleKit.Bundles
{
    public class Catalog : ScriptableObject
    {
        public List<AssetMap> Assets;

        public void Initialize()
        {
            if (!EditorUtility.IsPersistent(this)) return;
            var path = AssetDatabase.GetAssetPath(this);

            var am = new AssetsManager();
            var classDataPath = Path.Combine("Packages", "com.passivepicasso.bundlekit", "Library", "classdata.tpk");
            am.LoadClassPackage(classDataPath);
            am.LoadClassDatabaseFromPackage(Application.unityVersion);

            var (bun, bundleAssetsFile, assetBundleExtAsset) = am.LoadBundle(path);

            var bundleBaseField = assetBundleExtAsset.instance.GetBaseField();
            var bundleName = bundleBaseField.GetValue("m_AssetBundleName").AsString();

            am.UnloadAll();

            var allBundles = AssetBundle.GetAllLoadedAssetBundles().ToArray();
            var bundle = allBundles.FirstOrDefault(bnd => bnd.name.Equals(bundleName));
            if (!bundle)
                bundle = AssetBundle.LoadFromFile(path);

            var shaders = bundle.LoadAllAssets<Shader>();
            foreach (var shader in shaders)
            {
                ShaderUtil.RegisterShader(shader);
                switch (shader.name)
                {
                    case "Hidden/Internal-DeferredReflections":
                        GraphicsSettings.SetCustomShader(BuiltinShaderType.DeferredReflections, shader);
                        break;
                    case "Hidden/Internal-DeferredShading":
                        GraphicsSettings.SetCustomShader(BuiltinShaderType.DeferredShading, shader);
                        break;
                }
            }
            Assets = bundle.LoadAllAssets().Select(asset =>
            {
                var found = AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long loadedId);
                return new AssetMap(asset, guid, loadedId);
            }).ToList();

            hideFlags = HideFlags.None;
        }
        private void Awake()
        {
            Initialize();
        }
    }
}