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
        AssetBundle bundle;
        [InitializeOnLoadMethod]
        static void ReloadAssetsReferenceBundles()
        {
            var arbs = AssetDatabase.FindAssets($"t:{nameof(AssetsReferenceBundle)}")
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Select(path => AssetDatabase.LoadAssetAtPath<AssetsReferenceBundle>(path));

            foreach (var arb in arbs)
            {
                arb.Initialize();

            }
        }

        private void Initialize()
        {
            if (!EditorUtility.IsPersistent(this)) return;
            var path = AssetDatabase.GetAssetPath(this);
            var allBundles = AssetBundle.GetAllLoadedAssetBundles().ToArray();
            bundle = allBundles
                .FirstOrDefault(bnd => bnd.name.Contains(name));
            if (!bundle)
                bundle = AssetBundle.LoadFromFile(path);

            hideFlags = HideFlags.None;
        }

        private void Awake()
        {
            if (Assets == null) return;
            Initialize();
            LoadedIds = new long[Assets.Length];
            for (int i = 0; i < Assets.Length; i++)
            {
                var found = AssetDatabase.TryGetGUIDAndLocalFileIdentifier(Assets[i], out string guid, out long loadedId);
                LoadedIds[i] = loadedId;
            }
        }
    }
}