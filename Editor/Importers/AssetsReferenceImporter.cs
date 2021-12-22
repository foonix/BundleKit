using AssetsTools.NET.Extra;
using BundleKit.Assets;
using BundleKit.PipelineJobs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ThunderKit.Common.Package;
using UnityEditor;
using UnityEngine;
using UnityEditor.Experimental.AssetImporters;

namespace BundleKit.Bundles
{
    using static HideFlags;
    [ScriptedImporter(7, new[] { Extension })]
    public class AssetsReferenceImporter : ScriptedImporter
    {
        public const string Extension = "assetsreference";

        public MaterialDefinition[] customMaterialDefinitions;

        public override void OnImportAsset(AssetImportContext ctx)
        {
            var am = new AssetsManager();
            am.PrepareNewBundle(ctx.assetPath, out var bun, out var bundleAssetsFile, out var assetBundleExtAsset);

            var filefileDepDeps = assetBundleExtAsset.file.file.dependencies.dependencies;
            var filefileDepDepNames = filefileDepDeps.Select(inst => inst.assetPath).ToArray();
            am.UnloadAll();
            foreach (var ffddName in filefileDepDepNames)
            {
                if (ffddName == "unity_builtin_extra" || ffddName == "unity default resources")
                    continue;
                ctx.DependsOnSourceAsset($"Assets/{ffddName}.assetsreference");
            }

            var bundleName = Path.GetFileName(ctx.assetPath);
            var bundle = AssetBundle.GetAllLoadedAssetBundles().FirstOrDefault(bnd => ctx.assetPath.Contains(bnd.name));
            bundle?.Unload(false);
            try
            {
                bundle = AssetBundle.LoadFromFile(ctx.assetPath);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load: {ctx.assetPath}");
            }
            bundle.hideFlags = HideAndDontSave | DontSaveInBuild;

            var bundleAsset = ScriptableObject.CreateInstance<AssetsReferenceBundle>();
            bundleAsset.name = Path.GetFileNameWithoutExtension(ctx.assetPath);
            ctx.AddObjectToAsset(bundleAsset.name, bundleAsset);
            ctx.SetMainObject(bundleAsset);

            var allLoadedAssets = bundle.LoadAllAssets();
            bundleAsset.Assets = allLoadedAssets.OrderBy(a =>
            {
                var foundInfo = AssetDatabase.TryGetGUIDAndLocalFileIdentifier(a, out string guid, out long localId);
                return localId;
            }).ToArray();
            bundleAsset.LocalIds = new long[bundleAsset.Assets.Length];

            var textureLookup = new Dictionary<long, Texture>();
            for (int i = 0; i < bundleAsset.Assets.Length; i++)
            {
                var asset = bundleAsset.Assets[i];
                var foundInfo = AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId);
                bundleAsset.LocalIds[i] = localId;
                if (asset is Shader shader)
                {
                    ShaderUtil.RegisterShader(shader);
                    continue;
                }
                if (asset is Texture2D)
                {
                    if (foundInfo)
                        textureLookup[localId] = asset as Texture;
                }
                ctx.AddObjectToAsset(asset.name, asset);
            }
            if (customMaterialDefinitions != null && customMaterialDefinitions.Any())
            {
                var validDefinitions = customMaterialDefinitions.Where(md => !string.IsNullOrEmpty(md.name) && !string.IsNullOrEmpty(md.shader)).ToArray();
                var customMaterials = new Material[validDefinitions.Length];
                for (int i = 0; i < validDefinitions.Length; i++)
                {
                    customMaterials[i] = new Material(Shader.Find(validDefinitions[i].shader))
                    {
                        name = $"{validDefinitions[i].name} (Custom Asset)",
                        hideFlags = None
                    };

                    var nameHash = PackageHelper.GetStringHash(customMaterials[i].name);
                    var metaDataPath = Path.Combine("Library", "BundleKitMetaData", $"{nameHash}.json");
                    if (File.Exists(metaDataPath))
                    {
                        var jsonData = File.ReadAllText(metaDataPath);
                        var shaderData = JsonUtility.FromJson<SerializableMaterialData>(jsonData);
                        shaderData.Apply(customMaterials[i], textureLookup);
                    }
                    ctx.AddObjectToAsset(customMaterials[i].name, customMaterials[i]);
                }
                bundleAsset.CustomMaterials = customMaterials;
            }
        }
    }
}