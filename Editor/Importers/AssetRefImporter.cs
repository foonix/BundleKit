using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.AssetImporters;
using UnityEngine;

namespace BundleKit.Bundles
{
    [ScriptedImporter(1, extension)]
    public class AssetRefImporter : ScriptedImporter
    {
        public const string extension = "AssetRef";

        [MenuItem("Assets/ThunderKit/Create/Asset Reference", priority = 1)]
        public static void CreateMaterialReference()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);

            if (string.IsNullOrEmpty(path))
                path = "Assets";

            else if (!string.IsNullOrEmpty(Path.GetExtension(path)))
                path = path.Replace(Path.GetFileName(AssetDatabase.GetAssetPath(Selection.activeObject)), "");

            string assetPathAndName = AssetDatabase.GenerateUniqueAssetPath($"{path}/reference_asset.{extension}");
            File.WriteAllText(assetPathAndName, string.Empty);
            AssetDatabase.ImportAsset(assetPathAndName);
        }

        public override void OnImportAsset(AssetImportContext ctx)
        {
            var name = Path.GetFileNameWithoutExtension(ctx.assetPath);
            var bundle = AssetBundle.GetAllLoadedAssetBundles().FirstOrDefault(bun => bun.GetAllAssetNames().Any(n => n.Contains(name)));
            if (bundle)
            {
                var asset = bundle.LoadAllAssets().FirstOrDefault(obj => obj.name.Equals(name, System.StringComparison.OrdinalIgnoreCase));
                if (asset)
                {
                    ctx.AddObjectToAsset(System.Guid.NewGuid().ToString("x"), asset);
                    ctx.SetMainObject(asset);
                }
                bundle.Unload(true);
            }
        }
    }
}