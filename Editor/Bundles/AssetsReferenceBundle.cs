using AssetsTools.NET.Extra;
using BundleKit.PipelineJobs;
using BundleKit.Utility;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BundleKit.Bundles
{
    public class AssetsReferenceBundle : ScriptableObject
    {
        public Material[] CustomMaterials;
        public Object[] Assets;
        public long[] LocalIds;
        public long[] LoadedIds;
        public string[] dependencyNames;
        public Object[] References => Assets;

        public AssetsReferenceBundle[] dependencies;

        AssetBundle bundle;

        public void Initialize()
        {
            if (!EditorUtility.IsPersistent(this)) return;
            var path = AssetDatabase.GetAssetPath(this);

            var am = new AssetsManager();
            var classDataPath = Path.Combine("Packages", "com.passivepicasso.bundlekit", "Library", "classdata.tpk");
            am.LoadClassPackage(classDataPath);
            am.LoadClassDatabaseFromPackage(Application.unityVersion);

            am.PrepareNewBundle(path, out var bun, out var bundleAssetsFile, out var assetBundleExtAsset);

            var bundleBaseField = assetBundleExtAsset.instance.GetBaseField();
            var dependencyArray = bundleBaseField.GetField("m_Dependencies/Array");
            var dependencies = dependencyArray.GetChildrenList().Select(dep => dep.GetValue().AsString()).ToArray();
            var bundleName = bundleBaseField.GetValue("m_AssetBundleName").AsString();

            am.UnloadAll();

            var allBundles = AssetBundle.GetAllLoadedAssetBundles().ToArray();
            bundle = allBundles.FirstOrDefault(bnd => bnd.name.Equals(bundleName));
            if (!bundle)
                bundle = AssetBundle.LoadFromFile(path);

            Assets = bundle.LoadAllAssets();
            var shaders = bundle.LoadAllAssets<Shader>();
            foreach (var shader in shaders)
            {
                ShaderUtil.RegisterShader(shader);
                if (name.Contains("globalgamemanagers"))
                {
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
            }

            LoadedIds = new long[Assets.Length];
            for (int i = 0; i < Assets.Length; i++)
            {
                var found = AssetDatabase.TryGetGUIDAndLocalFileIdentifier(Assets[i], out string guid, out long loadedId);
                LoadedIds[i] = loadedId;
            }

            hideFlags = HideFlags.None;
        }

        private void Awake()
        {
            Initialize();
        }
    }
}