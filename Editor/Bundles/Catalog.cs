using AssetsTools.NET.Extra;
using BundleKit.Assets;
using BundleKit.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

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

            var bundleBaseField = assetBundleExtAsset.baseField;
            var bundleName = bundleBaseField["m_AssetBundleName"].AsString;

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
            var fileMapJson = bundle.LoadAsset<TextAsset>("FileMap");
            var fileMap = JsonUtility.FromJson<FileMap>(fileMapJson.text);
            var lookup = fileMap.Maps.ToDictionary(element => element.LocalId, element => element.OriginId);

            var allMyChildren = AssetDatabase.LoadAllAssetRepresentationsAtPath(AssetDatabase.GetAssetPath(this));
            Object[] allAssets = bundle.LoadAllAssets();
            foreach (var asset in allAssets)
            {
                if (asset.name == "FileMap") continue;
                var found = AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string name, out long localId);
                long sourceId = localId;
                if (lookup.ContainsKey(localId))
                {
                    name = lookup[localId].fileName;
                    sourceId = lookup[localId].localId;
                }
                var childAsset = allMyChildren.FirstOrDefault(a => a.name == asset.name);

                Assets.Add(new AssetMap(asset, childAsset, name, localId, sourceId));
            }
            Assets = Assets.OrderBy(asset => asset.localId).ToList();
            hideFlags = HideFlags.NotEditable;
        }
        private void Awake()
        {
            Initialize();
        }
    }
}