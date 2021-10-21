using System.Collections.Generic;
using ThunderKit.Core.Paths;
using UnityEditor;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;

namespace InBundleResourceReference.Editor.Building
{
    public class ExternalPackedIdentifiers : Unity5PackedIdentifiers
    {
        private readonly Dictionary<string, string> bundleToInternalName = new Dictionary<string, string>();
        private readonly string assetsPath;

        public ExternalPackedIdentifiers(ExternalReferenceAssets externalReferenceAssets)
        {
            assetsPath = Constants.ExternalReferenceAssetsPath.Resolve(null, null);
            foreach (var file in externalReferenceAssets.files)
            {
                bundleToInternalName[$"{Constants.AssetBundlePrefix}{file.fileName}"] = file.dependencyString;
            }
        }

        public override string GenerateInternalFileName(string name)
        {
            if (bundleToInternalName.TryGetValue(name, out var internalName))
            {
                return internalName;
            }
            return base.GenerateInternalFileName(name);
        }

        public override long SerializationIndexFromObjectIdentifier(ObjectIdentifier objectID)
        {
            var path = AssetDatabase.GUIDToAssetPath(objectID.guid.ToString());
            if (path.StartsWith(assetsPath))
            {
                return objectID.localIdentifierInFile;
            }
            return base.SerializationIndexFromObjectIdentifier(objectID);
        }
    }
}
