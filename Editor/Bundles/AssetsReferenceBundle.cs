using System.Linq;
using UnityEditor;
using UnityEngine;

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
        [InitializeOnLoadMethod]
        static void ReloadAssetsReferenceBundles()
        {
            var arbs = AssetDatabase.FindAssets($"t:{nameof(AssetsReferenceBundle)}")
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Select(path => AssetDatabase.LoadAssetAtPath<AssetsReferenceBundle>(path))
                .OrderBy(arb => arb.name)
                .ToArray();
            for (int i = 0; i < arbs.Length; i++)
            {
                var a = arbs[i];
            }
            arbs.First().Initialize();
        }

        private void Initialize(AssetBundle[] allBundles = null)
        {
            if (!EditorUtility.IsPersistent(this)) return;
            var path = AssetDatabase.GetAssetPath(this);
            if (allBundles == null)
                allBundles = AssetBundle.GetAllLoadedAssetBundles().ToArray();
            bundle = allBundles
                .FirstOrDefault(bnd => bnd.name.Contains(name));
            if (!bundle)
                bundle = AssetBundle.LoadFromFile(path);
            var shaders = bundle.LoadAllAssets<Shader>();
            foreach (var shader in shaders)
                ShaderUtil.RegisterShader(shader);

            hideFlags = HideFlags.None;
        }

        private void Awake()
        {
            if (Assets == null) return;
            LoadedIds = new long[Assets.Length];
            for (int i = 0; i < Assets.Length; i++)
            {
                var found = AssetDatabase.TryGetGUIDAndLocalFileIdentifier(Assets[i], out string guid, out long loadedId);
                LoadedIds[i] = loadedId;
            }
        }
    }
}