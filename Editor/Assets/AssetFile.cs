using System;

namespace BundleKit.Assets
{
    [Serializable]
    public class AssetFile
    {
        public string fileName;
        public bool isAssetBundle;
        public string dependencyString;
        public string relativeFolder;
    }
}