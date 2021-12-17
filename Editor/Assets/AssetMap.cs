using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace BundleKit.Assets
{
    [System.Serializable]
    [DebuggerDisplay("[({ResourcePointer.fileId}, {ResourcePointer.pathId}) <=> ({BundlePointer.fileId}, {BundlePointer.pathId})] {name}")]
    public struct AssetMap : IEquatable<AssetMap>
    {
        public string name;
        public AssetPointer ResourcePointer;
        public AssetPointer BundlePointer;

        public override bool Equals(object obj)
        {
            return obj is AssetMap map && Equals(map);
        }

        public bool Equals(AssetMap other)
        {
            return EqualityComparer<AssetPointer>.Default.Equals(ResourcePointer, other.ResourcePointer) &&
                   EqualityComparer<AssetPointer>.Default.Equals(BundlePointer, other.BundlePointer);
        }

        public override int GetHashCode()
        {
            int hashCode = 1567073812;
            hashCode = hashCode * -1521134295 + ResourcePointer.GetHashCode();
            hashCode = hashCode * -1521134295 + BundlePointer.GetHashCode();
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
