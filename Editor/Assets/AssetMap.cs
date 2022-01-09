using System;
using System.Collections.Generic;
using Object = UnityEngine.Object;

namespace BundleKit.Assets
{
    [Serializable]
    public struct AssetMap : IEquatable<AssetMap>
    {
        public Object asset;
        public string sourceFile;
        public long localId;

        public AssetMap(Object asset, string sourceFile, long localId)
        {
            this.asset = asset;
            this.sourceFile = sourceFile;
            this.localId = localId;
        }

        public void Deconstruct(out Object asset, out string sourceFile, out long localId)
        {
            asset = this.asset;
            sourceFile = this.sourceFile;
            localId = this.localId;
        }

        public override bool Equals(object obj)
        {
            return obj is AssetMap map && Equals(map);
        }

        public bool Equals(AssetMap other)
        {
            return sourceFile == other.sourceFile &&
                   localId == other.localId;
        }

        public override int GetHashCode()
        {
            int hashCode = -1047228845;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(sourceFile);
            hashCode = hashCode * -1521134295 + localId.GetHashCode();
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

        public static implicit operator (Object asset, string sourceFile, long localId)(AssetMap value)
        {
            return (value.asset, value.sourceFile, value.localId);
        }

        public static implicit operator AssetMap((Object asset, string sourceFile, long localId) value)
        {
            return new AssetMap(value.asset, value.sourceFile, value.localId);
        }
    }
}