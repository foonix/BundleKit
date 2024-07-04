using BundleKit.Bundles;
using System;
using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace BundleKit.Assets
{
    [ScriptedImporter(3, new[] { Extension })]
    public class BkAssetRefImporter : ScriptedImporter
    {
        public const string Extension = "bkassetref";

        public override void OnImportAsset(AssetImportContext ctx)
        {
            var assetRef = JsonUtility.FromJson<BkAssetRef>(File.ReadAllText(ctx.assetPath));
            var catalog = AssetDatabase.LoadAssetAtPath<Catalog>(assetRef.catalog);
            var refTargetAsset = catalog.GetAsset(assetRef.bundlePath, assetRef.GetRefTargetType());

            ctx.AddObjectToAsset($"{assetRef.catalog}/{assetRef.bundlePath}", refTargetAsset);
            ctx.SetMainObject(refTargetAsset);

            ctx.DependsOnArtifact(assetRef.catalog);
        }
    }
}
