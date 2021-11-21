using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.AssetImporters;
using UnityEngine;

namespace BundleKit.Bundles
{
    using static HideFlags;
    [ScriptedImporter(1, "assetsreference")]
    public class AssetsReferenceImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var bundleName = Path.GetFileName(ctx.assetPath);
            var bundle = AssetBundle.GetAllLoadedAssetBundles()
                .FirstOrDefault(bnd => ctx.assetPath.Contains(bnd.name));
            bundle?.Unload(true);
            bundle = AssetBundle.LoadFromFile(ctx.assetPath);

            var bundleAsset = ScriptableObject.CreateInstance<AssetsReferenceBundle>();
            bundleAsset.name = bundle.name;
            ctx.AddObjectToAsset(bundle.name, bundleAsset);
            ctx.SetMainObject(bundleAsset);

            var assets = bundle.LoadAllAssets().OfType<Material>().ToArray();
            for (int i = 0; i < assets.Length; i++)
            {
                var instance = Instantiate(assets[i]);
                instance.name = instance.name.Replace("(Clone)", " (Assets Reference)");
                instance.hideFlags = NotEditable;
                ctx.AddObjectToAsset(assets[i].name, instance);
                assets[i] = instance;
            }
            bundleAsset.Assets = assets;
        }
    }
}