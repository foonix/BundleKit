using System.IO;
using System.Linq;
using UnityEditor.Experimental.AssetImporters;
using UnityEngine;
using Exception = System.Exception;

namespace BundleKit.Bundles
{
    using static HideFlags;
    [ScriptedImporter(1, "assetsreference")]
    public class AssetsReferenceImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            //var am = new AssetsManager();
            var bundleName = Path.GetFileName(ctx.assetPath);
            var bundle = AssetBundle.GetAllLoadedAssetBundles().FirstOrDefault(bnd => ctx.assetPath.Contains(bnd.name));
            bundle?.Unload(true);
            bundle = AssetBundle.LoadFromFile(ctx.assetPath);
            bundle.hideFlags = HideAndDontSave | DontSaveInBuild;

            var bundleAsset = ScriptableObject.CreateInstance<AssetsReferenceBundle>();
            bundleAsset.name = bundle.name;
            ctx.AddObjectToAsset(bundle.name, bundleAsset, Texture2D.whiteTexture);
            ctx.SetMainObject(bundleAsset);

            Object[] allAssets = bundle.LoadAllAssets();
            try
            {
                var mappingDataJson = allAssets.OfType<TextAsset>().First(ta => ta.name == "mappingdata.json");
                //var data = JsonUtility.FromJson<MappingData>(mappingDataJson.text);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
            for (int i = 0; i < allAssets.Length; i++)
            {
                var instance = Instantiate(allAssets[i]);
                instance.name = instance.name.Replace("(Clone)", " (Assets Reference)");
                instance.hideFlags = NotEditable | DontSaveInBuild;
                ctx.AddObjectToAsset(allAssets[i].name, instance, Texture2D.whiteTexture);
                allAssets[i] = instance;
            }
            bundleAsset.Assets = allAssets;
        }
    }
}