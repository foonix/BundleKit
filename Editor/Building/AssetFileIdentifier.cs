using BundleKit.Bundles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Utilities;

namespace BundleKit.Building
{
    public class AssetFileIdentifier : IDeterministicIdentifiers
    {
        Dictionary<Catalog, Dictionary<long, long>> AssetLookup = new Dictionary<Catalog, Dictionary<long, long>>();
        public string GenerateInternalFileName(string name)
        {
            if (Path.GetExtension(name) == ".assets")
                return name;

            return "CAB-" + HashingMethods.Calculate<MD4>(name);
        }
        public long SerializationIndexFromObjectIdentifier(ObjectIdentifier objectID)
        {
            var path = AssetDatabase.GUIDToAssetPath(objectID.guid.ToString());
            var mainAsset = AssetDatabase.LoadMainAssetAtPath(path) as Catalog;

            if (mainAsset || (objectID.filePath.StartsWith("archive:/") && objectID.filePath.EndsWith(".assets")))
            {
                if (!mainAsset)
                {
                    var nameLength = objectID.filePath.Length - ("archive:/".Length + ".assets".Length);
                    var assetName = objectID.filePath.Substring("archive:/".Length, nameLength);
                    mainAsset = AssetDatabase.FindAssets($"{assetName} t:{nameof(Catalog)}").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Catalog>).FirstOrDefault();
                }
                if (mainAsset)
                {
                    if (!AssetLookup.ContainsKey(mainAsset))
                    {
                        //AssetLookup[mainAsset] = new Dictionary<long, long>();
                        //for (int i = 0; i < mainAsset.Assets.Count; i++)
                        //    AssetLookup[mainAsset].Add(mainAsset.LocalIds[i], mainAsset.Lo calIds[i]);
                    }
                    if (AssetLookup[mainAsset].ContainsKey(objectID.localIdentifierInFile))
                    {
                        long sourceId = AssetLookup[mainAsset][objectID.localIdentifierInFile];
                        return sourceId;
                    }
                }
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