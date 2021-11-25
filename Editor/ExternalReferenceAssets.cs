using BundleKit.Assets;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BundleKit
{
    public class ExternalReferenceAssets : ScriptableObject
    {
        public List<AssetFile> files = new List<AssetFile>();

        public static ExternalReferenceAssets GetOrCreate()
        {
            var assetsPath = Constants.ExternalReferenceAssetsPath;

            if (!Directory.Exists(assetsPath))
            {
                Directory.CreateDirectory(assetsPath);
            }

            var path = Path.Combine(assetsPath, $"{nameof(ExternalReferenceAssets)}.asset");
            var externalReferenceAssets = AssetDatabase.LoadAssetAtPath<ExternalReferenceAssets>(path);
            if (externalReferenceAssets != null)
            {
                return externalReferenceAssets;
            }

            externalReferenceAssets = CreateInstance<ExternalReferenceAssets>();
            externalReferenceAssets.name = nameof(ExternalReferenceAssets);
            AssetDatabase.CreateAsset(externalReferenceAssets, path);

            return externalReferenceAssets;
        }
    }
}