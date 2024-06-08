using BundleKit.Assets;
using System.IO;
using System.Linq;
using UnityEditor;
#if UNITY_2020_2_OR_NEWER
using UnityEditor.AssetImporters;
#else
using UnityEditor.Experimental.AssetImporters;
#endif

using UnityEngine;

namespace BundleKit.Bundles
{
    [ScriptedImporter(3, new[] { Extension })]
    public class SerializableMaterialDataImporter : ScriptedImporter
    {
        public const string Extension = "smd";

        [SerializeField]
        public string shader;

        public override void OnImportAsset(AssetImportContext ctx)
        {
            string json = File.ReadAllText(ctx.assetPath);
            var smd = JsonUtility.FromJson<SerializableMaterialData>(json);
            var material = smd.ToMaterial();

            var paths = material.GetTexturePropertyNames()
                .Select(tpm => (Object)material.GetTexture(tpm))
                .Prepend(material.shader)
                .Select(asset =>
                {
                    if (asset && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId))
                       return AssetDatabase.GUIDToAssetPath(guid);

                    return string.Empty;
                })
                .Distinct()
                .Where(path => !string.IsNullOrEmpty(path))
                .ToArray();

            foreach (var texDependency in paths)
                ctx.DependsOnSourceAsset(texDependency);

            ctx.AddObjectToAsset(smd.identity, material);
        }

        [MenuItem("Assets/BundleKit/Game Material")]
        static void CreateGameMaterial()
        {
            var path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (!AssetDatabase.IsValidFolder(path)) return;

            var shaders = AssetBundle.GetAllLoadedAssetBundles().SelectMany(bun => bun.LoadAllAssets<Shader>()).ToArray();
            var material = new Material(shaders[Random.Range(0, shaders.Length)]);
            var smd = SerializableMaterialData.Build(material);

            string assetPath = Path.Combine(path, $"NewMaterial.{Extension}");
            File.WriteAllText(assetPath, JsonUtility.ToJson(smd));

            AssetDatabase.ImportAsset(assetPath);
        }
    }
}
