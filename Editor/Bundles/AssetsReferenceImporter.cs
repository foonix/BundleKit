using BundleKit.Assets;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.AssetImporters;
using UnityEngine;
using Exception = System.Exception;

namespace BundleKit.Bundles
{
    using static HideFlags;
    [ScriptedImporter(4, new[] { "assetsreference" })]
    public class AssetsReferenceImporter : ScriptedImporter
    {
        public MaterialDefinition[] customMaterialDefinitions;
        public Material[] customMaterialsSerialization;

        public override void OnImportAsset(AssetImportContext ctx)
        {
            var bundleName = Path.GetFileName(ctx.assetPath);
            var bundle = AssetBundle.GetAllLoadedAssetBundles().FirstOrDefault(bnd => ctx.assetPath.Contains(bnd.name));
            bundle?.Unload(true);
            bundle = AssetBundle.LoadFromFile(ctx.assetPath);
            bundle.hideFlags = HideAndDontSave | DontSaveInBuild;

            var bundleAsset = ScriptableObject.CreateInstance<AssetsReferenceBundle>();
            bundleAsset.name = bundle.name;
            ctx.AddObjectToAsset(bundle.name, bundleAsset);
            ctx.SetMainObject(bundleAsset);

            Object[] allAssets = bundle.LoadAllAssets().OrderBy(a =>
            {
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(a, out string guid, out long localId))
                {
                    return localId;
                }
                return long.MaxValue;
            }).ToArray();
            try
            {
                var mappingDataJson = allAssets.OfType<TextAsset>().First(ta => ta.name == "mappingdata.json");
                //var data = JsonUtility.FromJson<MappingData>(mappingDataJson.text);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
            bundleAsset.Assets = allAssets;
            for (int i = 0; i < allAssets.Length; i++)
            {
                var asset = bundleAsset.Assets[i];
                asset.name = $"{asset.name} (Asset Reference)";
                if (asset is Shader shader)
                {
                    ShaderUtil.RegisterShader(shader);
                    continue;
                }
                if (asset is Texture2D)
                {
                    continue;
                }
                ctx.AddObjectToAsset(asset.name, asset);
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId))
                {
                    Debug.Log($"Found: {asset.name} Guid: {guid} PathId: {localId}");
                }
            }
            var validDefinitions = customMaterialDefinitions.Where(md => !string.IsNullOrEmpty(md.name) && !string.IsNullOrEmpty(md.shader)).ToArray();
            var customMaterials = new Material[validDefinitions.Length];
            for (int i = 0; i < validDefinitions.Length; i++)
            {
                customMaterials[i] = new Material(Shader.Find(validDefinitions[i].shader))
                {
                    name = $"{validDefinitions[i].name} (Custom Asset)",
                    hideFlags = None
                };
                ctx.AddObjectToAsset(customMaterials[i].name, customMaterials[i]);
            }
            bundleAsset.CustomMaterials = customMaterials;
        }
    }
}