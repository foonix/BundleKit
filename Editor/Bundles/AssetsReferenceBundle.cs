using BundleKit.Assets;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BundleKit.Bundles
{
    public class AssetsReferenceBundle : ScriptableObject
    {
        public Material[] CustomMaterials;
        public Object[] Assets;
        public MappingData mappingData;

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
            var bundle = allBundles
                .FirstOrDefault(bnd => bnd.name.Contains(name));
            if (!bundle)
                bundle = AssetBundle.LoadFromFile(path);
            hideFlags = HideFlags.None;
        }
    }
}