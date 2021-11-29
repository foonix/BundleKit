using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BundleKit.Utility
{
    public static class Constants
    {
        public static string[] ExcludedExtensions { get; } = new[] { ".dll", ".cs", ".meta" };
        public static string AssetBundlePrefix { get; } = "inbundleresourcereference_";
        public static string ExternalReferenceAssetsPath { get; } = Path.Combine("Assets", "ExternalReferenceAssets");
    }
}