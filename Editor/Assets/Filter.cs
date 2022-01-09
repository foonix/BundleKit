using AssetsTools.NET.Extra;
using System;

namespace BundleKit.Assets
{
    [Serializable]
    public struct Filter
    {
        public string[] nameRegex;
        public AssetClassID assetClass;
    }
}