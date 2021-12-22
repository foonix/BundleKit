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
        public Object[] References => Assets;

        public AssetsReferenceBundle[] dependencies;

        AssetBundle bundle;

        public void Initialize()
        {
            if (!EditorUtility.IsPersistent(this)) return;
            var path = AssetDatabase.GetAssetPath(this);
            var allBundles = AssetBundle.GetAllLoadedAssetBundles().ToArray();
            bundle = allBundles.FirstOrDefault(bnd => bnd.name.Contains(name));
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