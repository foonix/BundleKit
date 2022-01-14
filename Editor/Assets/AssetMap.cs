using System;
using System.Collections.Generic;
using Object = UnityEngine.Object;

namespace BundleKit.Assets
{
    [Serializable]
    public struct AssetMap : IEquatable<AssetMap>
    {
        public Object internalAsset;
        public Object externalAsset;
        public string sourceFile;
        public long localId;
        public long sourceId;

        public AssetMap(Object internalAsset, Object externalAsset, string sourceFile, long localId, long sourceId)
        {
            this.internalAsset = internalAsset;
            this.externalAsset = externalAsset;
            this.sourceFile = sourceFile;
            this.sourceId = sourceId;
            this.localId = localId;
        }

        public override bool Equals(object obj)
        {
            return obj is AssetMap map && Equals(map);
        }

        public bool Equals(AssetMap other)
        {
            return sourceFile == other.sourceFile &&
                   sourceId == other.sourceId;
        }

        public override int GetHashCode()
        {
            int hashCode = -1047228845;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(sourceFile);
            hashCode = hashCode * -1521134295 + sourceId.GetHashCode();
            return hashCode;
        }

        public static bool operator ==(AssetMap left, AssetMap right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AssetMap left, AssetMap right)
        {
            return !(left == right);
        }

    }
}