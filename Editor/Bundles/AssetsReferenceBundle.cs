using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BundleKit.Bundles
{
    public class AssetsReferenceBundle : ScriptableObject
    {
        public Object[] Assets;

        [InitializeOnLoadMethod]
        static void ReloadAssetsReferenceBundles()
        {
            var arbs = AssetDatabase.FindAssets($"t:{nameof(AssetsReferenceBundle)}")
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Select(path => AssetDatabase.LoadAssetAtPath<AssetsReferenceBundle>(path));
            foreach (var arb in arbs)
                arb.Initialize();
        }

        private void Initialize()
        {
            if (!EditorUtility.IsPersistent(this)) return;
            var path = AssetDatabase.GetAssetPath(this);
            var bundle = AssetBundle.GetAllLoadedAssetBundles().FirstOrDefault(bnd => name == bnd.name);
            if (!bundle)
                bundle = AssetBundle.LoadFromFile(path);
        }
    }
}