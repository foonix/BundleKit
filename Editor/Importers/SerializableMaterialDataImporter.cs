using BundleKit.Assets;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.AssetImporters;
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

            var material = new Material(Shader.Find("Standard"));
            var localTextures = new Dictionary<long, Texture>();
            smd.Apply(material, localTextures);
            ctx.AddObjectToAsset(smd.identity, material);
        }

        [MenuItem("Assets/BundleKit/Game Material")]
        static  void CreateGameMaterial()
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
