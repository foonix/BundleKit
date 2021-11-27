using AssetsTools.NET.Extra;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.AssetImporters;
using UnityEngine;
using Exception = System.Exception;

namespace BundleKit.Bundles
{
    using static HideFlags;
    [ScriptedImporter(1, "assets")]
    public class AssetsImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(ctx.assetPath);

            for (int i = 0; i < allAssets.Length; i++)
            {
                var instance = Instantiate(allAssets[i]);
                instance.name = instance.name.Replace("(Clone)", " (Assets Reference)");
                instance.hideFlags = NotEditable | DontSaveInBuild;
                ctx.AddObjectToAsset(allAssets[i].name, instance);
                allAssets[i] = instance;
            }
        }
    }
}