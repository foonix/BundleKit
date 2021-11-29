using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;

namespace BundleKit.Bundles
{
    public class AssetsReferenceBundle : ScriptableObject
    {
        public Material[] CustomMaterials;
        public Object[] Assets;

        public List<(long, long)> mappings = new List<(long, long)>();
        public long this[long pathId]
        {
            get
            {
                return mappings.FirstOrDefault(m => m.Item1 == pathId).Item2;
            }
        }

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