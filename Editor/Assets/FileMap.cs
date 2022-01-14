using System;
using System.Collections.Generic;

namespace BundleKit.Assets
{
    [Serializable]
    public struct FileMap
    {
        public MapRecord[] Maps;
    }

    [Serializable]
    public struct MapRecord
    {
        public long LocalId;
        public AssetId OriginId;

        public MapRecord(long localId, AssetId originId)
        {
            LocalId = localId;
            OriginId = originId;
        }

        public override bool Equals(object obj)
        {
            return obj is MapRecord other &&
                   LocalId == other.LocalId &&
                   OriginId.Equals(other.OriginId);
        }

        public override int GetHashCode()
        {
            int hashCode = -1030903623;
            hashCode = hashCode * -1521134295 + LocalId.GetHashCode();
            hashCode = hashCode * -1521134295 + OriginId.GetHashCode();
            return hashCode;
        }

        public void Deconstruct(out long localId, out (string fileName, long localId) originId)
        {
            localId = LocalId;
            originId = OriginId;
        }

        public static implicit operator (long localId, (string fileName, long localId) originId)(MapRecord value)
        {
            return (value.LocalId, value.OriginId);
        }

        public static implicit operator MapRecord((long localId, (string fileName, long localId) originId) value)
        {
            return new MapRecord(value.localId, value.originId);
        }

        public static implicit operator (long localId, AssetId originId)(MapRecord value)
        {
            return (value.LocalId, value.OriginId);
        }

        public static implicit operator MapRecord((long localId, AssetId originId) value)
        {
            return new MapRecord(value.localId, value.originId);
        }
    }

    [Serializable]
    public struct AssetId
    {
        public string fileName;
        public long localId;

        public AssetId(string fileName, long localId)
        {
            this.fileName = fileName;
            this.localId = localId;
        }

        public override bool Equals(object obj)
        {
            return obj is AssetId other &&
                   fileName == other.fileName &&
                   localId == other.localId;
        }

        public override int GetHashCode()
        {
            int hashCode = 1181198205;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(fileName);
            hashCode = hashCode * -1521134295 + localId.GetHashCode();
            return hashCode;
        }

        public void Deconstruct(out string fileName, out long localId)
        {
            fileName = this.fileName;
            localId = this.localId;
        }

        public static implicit operator (string fileName, long localId)(AssetId value)
        {
            return (value.fileName, value.localId);
        }

        public static implicit operator AssetId((string fileName, long localId) value)
        {
            return new AssetId(value.fileName, value.localId);
        }
    }
}