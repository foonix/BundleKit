using AssetsTools.NET.Extra;
using System.Collections.Generic;
using System.Diagnostics;

namespace BundleKit.Assets
{
    [DebuggerDisplay("Name: [{AssetFileName}]{AssetName} FileID: ({FileId}) PathID: {PathId}")]
    public struct AssetData
    {
        public readonly AssetExternal AssetExt;
        public readonly string AssetName;
        public readonly string AssetFileName;
        public readonly int FileId;
        public readonly long PathId;
        public readonly int Depth;

        public AssetData(AssetExternal ext, string name, string assetFileName, int fileId, long pathId, int depth)
        {
            this.AssetExt = ext;
            this.AssetName = name;
            this.AssetFileName = assetFileName;
            this.FileId = fileId;
            this.PathId = pathId;
            this.Depth = depth;
        }

        public override bool Equals(object obj)
        {
            return obj is AssetData other &&
                   AssetName == other.AssetName &&
                   AssetFileName == other.AssetFileName &&
                   FileId == other.FileId &&
                   PathId == other.PathId &&
                   Depth == other.Depth;
        }

        public override int GetHashCode()
        {
            int hashCode = -359814420;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(AssetName);
            hashCode = hashCode * -1521134295 + FileId.GetHashCode();
            hashCode = hashCode * -1521134295 + PathId.GetHashCode();
            hashCode = hashCode * -1521134295 + Depth.GetHashCode();
            return hashCode;
        }

        public void Deconstruct(out AssetExternal ext, out string name, out string assetFileName, out int fileId, out long pathId, out int depth)
        {
            ext = this.AssetExt;
            name = this.AssetName;
            assetFileName = this.AssetFileName;
            fileId = this.FileId;
            pathId = this.PathId;
            depth = this.Depth;
        }

        public static implicit operator (AssetExternal ext, string name, string assetFileName, int fileId, long pathId, int depth)(AssetData value)
        {
            return (value.AssetExt, value.AssetName, value.AssetFileName, value.FileId, value.PathId, value.Depth);
        }

        public static implicit operator AssetData((AssetExternal ext, string name, string assetFileName, int fileId, long pathId, int depth) value)
        {
            return new AssetData(value.ext, value.name, value.assetFileName, value.fileId, value.pathId, value.depth);
        }
    }
}