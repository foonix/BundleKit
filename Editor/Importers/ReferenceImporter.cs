using BundleKit.Assets;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEditor.Experimental.AssetImporters;
using UnityEngine;

namespace BundleKit.Bundles
{
    [ScriptedImporter(3, new[] { Extension })]
    public class ReferenceImporter : ScriptedImporter
    {
        public const string Extension = "reference";

        public override void OnImportAsset(AssetImportContext ctx)
        {
            var name = Path.GetFileNameWithoutExtension(ctx.assetPath);
            var referenceJson = File.ReadAllText(ctx.assetPath);
            var reference = ScriptableObject.CreateInstance<Reference>();
            JsonUtility.FromJsonOverwrite(referenceJson, reference);

            ctx.AddObjectToAsset(name, reference);
            ctx.SetMainObject(reference);

            var localPath = Path.GetDirectoryName(ctx.assetPath);

            foreach (var bundle in AssetBundle.GetAllLoadedAssetBundles())
            {
                if (!reference.AssetBundles.Contains(bundle.name)) continue;
                bundle.Unload(true);
            }
            var bundles = new List<AssetBundle>();
            foreach (var bundleName in reference.AssetBundles)
            {
                var bundlePath = Path.Combine(localPath, bundleName);
                bundles.Add(AssetBundle.LoadFromFile(bundlePath));
            }

            var textureLookup = new Dictionary<(string, long), Texture>();
            foreach (var bun in bundles)
            {
                var root = bun.name;
                var allAssets = bun.LoadAllAssets();

                for (int i = 0; i < allAssets.Length; i++)
                {
                    var asset = allAssets[i];
                    var foundInfo = AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId);
                    if (asset is Shader shader)
                    {
                        ShaderUtil.RegisterShader(shader);
                        continue;
                    }
                    if (asset is Texture2D)
                    {
                        if (foundInfo)
                            textureLookup[(root, localId)] = asset as Texture;
                    }
                    asset.name = $"{root}/{asset.name}";
                    var identifier = HashingMethods.Calculate<MD4>(asset.name).ToString();
                    ctx.AddObjectToAsset(identifier, asset);
                }
            }
        }
    }
}