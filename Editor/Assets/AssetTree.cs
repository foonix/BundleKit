using AssetsTools.NET.Extra;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace BundleKit.Assets
{
    [DebuggerDisplay("{assetExternal.file.name}/{name} fid:{FileId} pid:{PathId} children: {Children.Count}")]
    public struct AssetTree : IEquatable<AssetTree>
    {
        public string name;
        public string resourceManagerName;
        public AssetExternal sourceData;
        public int FileId;
        public long PathId;
        /// <summary>
        /// List of dependencies in the dependency graph for the object has been resolved.
        /// Null if dependencies are unknown.
        /// </summary>
        public List<AssetTree> Children;

        public IEnumerable<(int fileId, long pathId)> FlattenIds(bool enterDependencies)
        {
            yield return (FileId, PathId);
            foreach (var child in Children)
                if (enterDependencies || child.FileId == 0)
                    foreach (var result in child.FlattenIds(enterDependencies))
                        yield return result;
        }
        public IEnumerable<AssetTree> WithDeps(bool enterDependencies)
        {
            yield return this;
            if (enterDependencies)
                foreach (var child in Children)
                    yield return child;
        }

        public string GetBkCatalogName()
        {
            return string.IsNullOrEmpty(resourceManagerName) ? name.ToLower() : resourceManagerName;
        }

        public override bool Equals(object obj)
        {
            return obj is AssetTree tree && Equals(tree);
        }

        public bool Equals(AssetTree other)
        {
            return EqualityComparer<AssetsFileInstance>.Default.Equals(sourceData.file, other.sourceData.file) &&
                   PathId == other.PathId;
        }

        public override int GetHashCode()
        {
            int hashCode = -1120199924;
            hashCode = hashCode * -1521134295 + EqualityComparer<AssetsFileInstance>.Default.GetHashCode(sourceData.file);
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
