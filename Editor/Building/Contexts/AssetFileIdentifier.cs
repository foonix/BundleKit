using BundleKit.Bundles;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Utilities;

namespace BundleKit.Building
{
    public class AssetFileIdentifier : IDeterministicIdentifiers
    {
        private Dictionary<long, long> indexMap;
        public AssetsReferenceBundle AssetsReferenceBundle { get; }

        public AssetFileIdentifier(AssetsReferenceBundle assetsReferenceBundle)
        {
            var assetMaps = assetsReferenceBundle.mappingData.AssetMaps;
            indexMap = new Dictionary<long, long>();
            for (int i = 0; i < assetsReferenceBundle.Assets.Length - 1; i++)
            {
                UnityEngine.Object obj = assetsReferenceBundle.Assets[i];
                Assets.AssetMap assetMap = assetMaps[i];
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out string _, out long pathId);
                indexMap[pathId] = assetMap.ResourcePointer.pathId;
            }
            AssetsReferenceBundle = assetsReferenceBundle;
        }


        public string GenerateInternalFileName(string name)
        {
            if (name == "resources.assets")
                return "resources.assets";

            return "CAB-" + HashingMethods.Calculate<MD4>(name);
        }
        public long SerializationIndexFromObjectIdentifier(ObjectIdentifier objectID)
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(AssetsReferenceBundle, out string arbGuid, out long arbLocalId);
            var path = AssetDatabase.GUIDToAssetPath(objectID.guid.ToString());
            var mainAsset = AssetDatabase.LoadMainAssetAtPath(path);
            if (arbGuid == objectID.guid.ToString() || objectID.guid == default || mainAsset is AssetsReferenceBundle)
            {
                var reps = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
                if (indexMap.ContainsKey(objectID.localIdentifierInFile))
                {
                    long localId = indexMap[objectID.localIdentifierInFile];
                    return localId;
                }
                else
                    return objectID.localIdentifierInFile;
            }
            else
            {
                RawHash hash;
                bool extraArtifact = objectID.filePath.StartsWith("VirtualArtifacts/Extra/", StringComparison.Ordinal);
                int hashSeed = 12;
                if (extraArtifact && hashSeed != 0)
                {
                    RawHash fileHash = HashingMethods.CalculateFile(objectID.filePath);
                    hash = HashingMethods.Calculate(hashSeed, fileHash, objectID.localIdentifierInFile);
                }
                else if (extraArtifact)
                {
                    RawHash fileHash = HashingMethods.CalculateFile(objectID.filePath);
                    hash = HashingMethods.Calculate(fileHash, objectID.localIdentifierInFile);
                }
                else if (hashSeed != 0)
                {
                    if (objectID.fileType == FileType.MetaAssetType || objectID.fileType == FileType.SerializedAssetType)
                        hash = HashingMethods.Calculate<MD4>(hashSeed, objectID.guid.ToString(), objectID.fileType, objectID.localIdentifierInFile);
                    else
                        hash = HashingMethods.Calculate<MD4>(hashSeed, objectID.filePath, objectID.localIdentifierInFile);
                }
                else
                {
                    if (objectID.fileType == FileType.MetaAssetType || objectID.fileType == FileType.SerializedAssetType)
                        hash = HashingMethods.Calculate<MD4>(objectID.guid.ToString(), objectID.fileType, objectID.localIdentifierInFile);
                    else
                        hash = HashingMethods.Calculate<MD4>(objectID.filePath, objectID.localIdentifierInFile);
                }

                return BitConverter.ToInt64(hash.ToBytes(), 0);
            }
        }
    }
}