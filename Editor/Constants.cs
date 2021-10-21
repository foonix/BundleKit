using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace InBundleResourceReference
{
    public static class Constants
    {
        public static string ModifiedExtension { get; } = ".modified";
        public static string[] ExcludedExtensions { get; } = new[] { ".dll", ".cs", ".meta" };
        public static string AssetBundlePrefix { get; } = "inbundleresourcereference_";
        public static string MapAssetBundleName { get; } = $"{AssetBundlePrefix}externalreferenceassets";
        public static string ExternalReferenceAssetsPath { get; } = "<ExternalReferenceAssets>";
    }
}