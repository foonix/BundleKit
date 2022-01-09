using AssetsTools.NET.Extra;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace BundleKit.Assets
{
    [DebuggerDisplay("{assetsFileInstance.name}/{name} fid:{FileId} pid:{PathId} children: {Children.Count}")]
    public struct AssetTree : IEquatable<AssetTree>
    {
        public string name;
        public AssetExternal assetExternal;
        public int FileId;
        public long PathId;
        public List<AssetTree> Children;

        public IEnumerable<(int fileId, long pathId)> FlattenIds(bool enterDependencies)
        {
            yield return (FileId, PathId);
            foreach (var child in Children)
                if (enterDependencies || child.FileId == 0)
                    foreach (var result in child.FlattenIds(enterDependencies))
                        yield return result;
        }
        public IEnumerable<AssetTree> Flatten(bool enterDependencies)
        {
            yield return this;
            foreach (var child in Children)
                if (enterDependencies || child.FileId == 0)
                    foreach (var result in child.Flatten(enterDependencies))
                        yield return result;
        }

        public override bool Equals(object obj)
        {
            return obj is AssetTree tree && Equals(tree);
        }

        public bool Equals(AssetTree other)
        {
            return EqualityComparer<AssetsFileInstance>.Default.Equals(assetExternal.file, other.assetExternal.file) &&
                   PathId == other.PathId;
        }

        public override int GetHashCode()
        {
            int hashCode = -1120199924;
            hashCode = hashCode * -1521134295 + EqualityComparer<AssetsFileInstance>.Default.GetHashCode(assetExternal.file);
            hashCode = hashCode * -1521134295 + PathId.GetHashCode();
            return hashCode;
        }

        public static bool operator ==(AssetTree left, AssetTree right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AssetTree left, AssetTree right)
        {
            return !(left == right);
        }
    }
}
