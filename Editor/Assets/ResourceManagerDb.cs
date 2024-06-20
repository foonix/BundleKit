using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System.Collections.Generic;

namespace BundleKit.Assets
{
    /// <summary>
    /// Provides data queries into ResourceManager.
    ///
    /// ResourceManager stores data for APIs such as Resources.Load() to function.
    /// It stores the file paths, preload tables, and dependency information.
    /// In a build, this data lives in globalgamemanagers.
    /// </summary>
    public class ResourceManagerDb
    {
        private readonly Dictionary<AssetId, string> assetToName = new();

        private const string ggmFileName = "globalgamemanagers";

        /// <summary>
        /// Create ResourceManagerDb from the provied AssetsManager.
        /// am must have already loaded globalgamemanagers.
        /// </summary>
        /// <param name="am">AssetsManager representing a game build, with globalgamemanagers file loaded.</param>
        public ResourceManagerDb(AssetsManager am)
        {
            var ggmFileInstance = am.FileLookup[ggmFileName];
            var rmInfo = ggmFileInstance.file.GetAssetsOfType(AssetClassID.ResourceManager)[0];
            var rmBase = am.GetBaseField(ggmFileInstance, rmInfo);
            var ggmExternals = ggmFileInstance.file.Metadata.Externals;

            ParseMContainer(rmBase, ggmExternals);

            // Skipping implementation for m_DependentAssets until it's needed
        }

        public string this[AssetId id] => assetToName[id];

        public bool TryGetName(AssetId id, out string resourceManagerName)
            => assetToName.TryGetValue(id, out resourceManagerName);

        public bool TryGetName(string assetsFileName, long pathIdInFile, out string resourceManagerName)
            => assetToName.TryGetValue(new AssetId(assetsFileName, pathIdInFile), out resourceManagerName);

        private void ParseMContainer(AssetTypeValueField rmBase, List<AssetsFileExternal> ggmExternals)
        {
            var mContainer = rmBase["m_Container"];
            foreach (var entry in mContainer.Children[0].Children)
            {
                string name = entry.Children[0].AsString;
                int pathId = entry.Children[1].Children[0].AsInt;
                long fileId = entry.Children[1].Children[1].AsLong;

                if (pathId == 0)
                {
                    // Some entries have m_FileID: 0, m_PathID: 0
                    // not sure what causes this, but may be a path for an asset that didn't go into the build.
                    continue;
                }

                string containerFileName;
                if (pathId > 0)
                {
                    containerFileName = ggmExternals[pathId - 1].OriginalPathName;
                }
                else
                {
                    containerFileName = ggmFileName;
                }

                assetToName.Add(new AssetId(containerFileName, fileId), name);
            }
        }
    }
}
