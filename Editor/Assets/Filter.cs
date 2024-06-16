using AssetsTools.NET.Extra;
using System;
using UnityEngine;

namespace BundleKit.Assets
{
    [Serializable]
    public struct Filter
    {
        [Tooltip("An object must have a name matching one of these expressions.  Leave this list empty to match any name.")]
        public string[] nameRegex;
        [Tooltip("Unity built-in object class required for this filter to match.  Use 'Object' to match any kind of object.")]
        public AssetClassID assetClass;
    }
}