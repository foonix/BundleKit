using System.Collections.Generic;
using System.IO;
using ThunderKit.Core.Paths;
using UnityEditor;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;

namespace InBundleResourceReference.Editor.Building
{
    public class ExternalPackedIdentifiers : Unity5PackedIdentifiers
    {
        private readonly Dictionary<string, string> bundleToInternalName = new Dictionary<string, string>();

        public ExternalPackedIdentifiers(ExternalReferenceAssets externalReferenceAssets)
        {
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
            if (Path.GetDirectoryName(path).StartsWith(Constants.ExternalReferenceAssetsPath))
            {
                return objectID.localIdentifierInFile;
            }
            return base.SerializationIndexFromObjectIdentifier(objectID);
        }
    }
}
