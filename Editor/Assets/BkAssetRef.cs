using System;

namespace BundleKit.Assets
{
    [Serializable]
    public class BkAssetRef
    {
        public string catalog;
        public string bundlePath;
        // AssemblyQualifiedName
        public string type;

        public override string ToString()
        {
            return $"BkAssetRef: {catalog}/{bundlePath} ({type})";
        }

        public Type GetRefTargetType()
        {
            return Type.GetType(type);
        }
    }
}