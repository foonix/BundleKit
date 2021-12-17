using System;
using System.Diagnostics;

namespace BundleKit.Assets
{
    [Serializable]
    [DebuggerDisplay("({fileId} : {pathId})")]
    public struct AssetPointer
    {
        public int fileId;
        public long pathId;

        public AssetPointer(int fileId, long pathId)
        {
            this.fileId = fileId;
            this.pathId = pathId;
        }

        public override bool Equals(object obj)
        {
            return obj is AssetPointer other &&
                   fileId == other.fileId &&
                   pathId == other.pathId;
        }

        public override int GetHashCode()
        {
            int hashCode = 1048910345;
            hashCode = hashCode * -1521134295 + fileId.GetHashCode();
            hashCode = hashCode * -1521134295 + pathId.GetHashCode();
            return hashCode;
        }

        public void Deconstruct(out int fileId, out long pathId)
        {
            fileId = this.fileId;
            pathId = this.pathId;
        }

        public static implicit operator (int fileId, long pathId)(AssetPointer value)
        {
            return (value.fileId, value.pathId);
        }

        public static implicit operator AssetPointer((int fileId, long pathId) value)
        {
            return new AssetPointer(value.fileId, value.pathId);
        }
    }
}