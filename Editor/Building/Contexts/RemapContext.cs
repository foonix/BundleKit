using BundleKit.Assets;
using System.Collections.Generic;
using UnityEditor.Build.Pipeline.Interfaces;

namespace BundleKit.Building.Contexts
{
    public interface IAssetMapsContext : IContextObject
    {
        List<AssetMap> AssetMaps { get; } 
    }
    public class RemapContext : IAssetMapsContext
    {
        public List<AssetMap> AssetMaps { get; private set; } = new List<AssetMap>();
    }
}