using AssetsTools.NET.Extra;
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
            var am = new AssetsManager();
            var bundleName = Path.GetFileName(ctx.assetPath);
            var bundle = AssetBundle.GetAllLoadedAssetBundles().FirstOrDefault(bnd => ctx.assetPath.Contains(bnd.name));
            bundle?.Unload(true);
            bundle = AssetBundle.LoadFromFile(ctx.assetPath);
            bundle.hideFlags = HideAndDontSave | DontSaveInBuild;

            var bundleAsset = ScriptableObject.CreateInstance<AssetsReferenceBundle>();
            //bundleAsset.hideFlags = NotEditable | DontSaveInBuild;
            bundleAsset.name = bundle.name;
            ctx.AddObjectToAsset(bundle.name, bundleAsset);
            ctx.SetMainObject(bundleAsset);

            var assets = bundle.LoadAllAssets().OfType<Material>().ToArray();
            for (int i = 0; i < assets.Length; i++)
            {
                var instance = Instantiate(assets[i]);
                instance.name = instance.name.Replace("(Clone)", " (Assets Reference)");
                instance.hideFlags = NotEditable | DontSaveInBuild;
                //var instanceId = instance.GetInstanceID();
                //if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(instance, out string guid, out long localId))
                //{
                //    Debug.Log($"name: {instance.name}  instance:{instanceId}   guid:{guid}   local:{localId}");
                //}

                ctx.AddObjectToAsset(assets[i].name, instance);
                assets[i] = instance;
            }
            bundleAsset.Assets = assets;
        }
    }
}