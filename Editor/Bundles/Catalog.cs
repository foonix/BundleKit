using AssetsTools.NET.Extra;
using BundleKit.Assets;
using BundleKit.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace BundleKit.Bundles
{
    public class Catalog : ScriptableObject
    {
        public List<AssetMap> Assets;

        public string bundlePath;
        public string bundleName;

        public void Initialize()
        {
            if (!EditorUtility.IsPersistent(this)) return;
            bundlePath = AssetDatabase.GetAssetPath(this);

            var am = new AssetsManager();
            var classDataPath = Path.Combine("Packages", "com.passivepicasso.bundlekit", "Library", "classdata.tpk");
            am.LoadClassPackage(classDataPath);
            am.LoadClassDatabaseFromPackage(Application.unityVersion);

            var (bun, bundleAssetsFile, assetBundleExtAsset) = am.LoadBundle(bundlePath);

            var bundleBaseField = assetBundleExtAsset.baseField;
            bundleName = bundleBaseField["m_AssetBundleName"].AsString;

            am.UnloadAll();

            AssetBundle bundle = GetOrLoadBundle(bundleName, bundlePath);

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
            var fileMapJson = bundle.LoadAsset<TextAsset>("BundleKitFileMap");
            var fileMap = JsonUtility.FromJson<FileMap>(fileMapJson.text);
            var lookup = fileMap.Maps.ToDictionary(element => element.LocalId, element => element.OriginId);

            var allMyChildren = AssetDatabase.LoadAllAssetRepresentationsAtPath(AssetDatabase.GetAssetPath(this));
            Object[] allAssets = bundle.LoadAllAssets();
            foreach (var asset in allAssets)
            {
                if (asset.name == "BundleKitFileMap") continue;
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

        private void OnEnable()
        {
            Initialize();
        }

        public Object GetAssetByLocalPathid(long localPathId)
        {
            var bundle = GetOrLoadBundle(bundleName, bundlePath);

            var assetMap = Assets.FirstOrDefault(a => a.localId == localPathId);

            return assetMap.internalAsset;
        }

        public T GetAsset<T>(string bundlePath) where T : Object
        {
            var bundle = GetOrLoadBundle(bundleName, bundlePath);
            return bundle.LoadAsset<T>(bundlePath);
        }

        public Object GetAsset(string bundlePath, Type type)
        {
            var bundle = GetOrLoadBundle(bundleName, bundlePath);
            return bundle.LoadAsset(bundlePath, type);
        }

        public string FindObjectPathWithName<T>(string name) where T : UnityEngine.Object
        {
            var bundle = GetOrLoadBundle(bundleName, bundlePath);
            foreach (var path in bundle.GetAllAssetNames())
            {
                var obj = bundle.LoadAsset<T>(path);
                if (obj == null)
                {
                    continue;
                }

                if (obj.name == name)
                {
                    return path;
                }
            }

            return null;
        }

        private AssetBundle GetOrLoadBundle(string bundleName, string loadPath)
        {
            var bundle = AssetBundle.GetAllLoadedAssetBundles()
                .FirstOrDefault(bnd => bnd.name.Equals(bundleName));

            if (!bundle)
            {
                bundle = AssetBundle.LoadFromFile(loadPath);
            }

            return bundle;
        }

        [MenuItem("CONTEXT/Catalog/Create BundleKit Asset Reference")]
        public static void CreateAssetRef(MenuCommand command)
        {
            var path = AssetDatabase.GetAssetPath(command.context);
            var dir = Path.GetDirectoryName(path);
            var assetRef = new BkAssetRef()
            {
                catalog = path,
                bundlePath = "",
            };

            string assetPath = Path.Combine(dir, $"NewAssetRef.{BkAssetRefImporter.Extension}");
            File.WriteAllText(assetPath, JsonUtility.ToJson(assetRef, true));

            AssetDatabase.ImportAsset(assetPath);
        }
    }
}