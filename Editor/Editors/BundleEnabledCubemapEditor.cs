//using BundleKit.Assets;
//using BundleKit.Bundles;
//using System.IO;
//using System.Linq;
//using ThunderKit.Common.Package;
//using UnityEditor;
//using UnityEngine;
//using static UnityEditor.EditorGUI;

//namespace BundleKit.Editors
//{
//    [CustomEditor(typeof(Cubemap))]
//    public class BundleEnabledCubemapEditor : Editor
//    {
//        protected override void OnHeaderGUI()
//        {
//            string assetPath = AssetDatabase.GetAssetPath(target);
//            var importer = AssetImporter.GetAtPath(assetPath);
//            if (importer is SerializableMaterialDataImporter smdi)
//            {
//                using (new DisabledScope(false))
//                    base.OnHeaderGUI();
//                return;
//            }
//            var mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
//            if (mainAsset is AssetsReferenceBundle arb)
//            {
//                if (arb.CustomMaterials.Contains(target))
//                {
//                    //using (new DisabledScope(true))
//                    base.OnHeaderGUI();

//                    return;
//                }
//            }

//            base.OnHeaderGUI();
//        }
//        public override void OnInspectorGUI()
//        {
//            string assetPath = AssetDatabase.GetAssetPath(target);
//            var mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
//            var arb = mainAsset as AssetsReferenceBundle;
//            if (arb)
//            {
//                if (arb.CustomMaterials.Contains(target))
//                {
//                    target.hideFlags = HideFlags.None;
//                }
//            }
//            var smdi = AssetImporter.GetAtPath(assetPath) as SerializableMaterialDataImporter;
//            if (smdi)
//            {
//                target.hideFlags = HideFlags.None;
//            }
//            using (new DisabledScope(false))
//                base.OnInspectorGUI();
//            if (smdi)
//            {
//                var material = target as Material;
//                var smd = JsonUtility.FromJson<SerializableMaterialData>(File.ReadAllText(assetPath));
//                var shaderData = SerializableMaterialData.Build(material, smd.identity);
//                var jsonData = JsonUtility.ToJson(shaderData, true);
//                File.WriteAllText(assetPath, jsonData);
//            }
//            if (arb)
//            {
//                if (arb.CustomMaterials.Contains(target))
//                {
//                    var material = target as Material;
//                    var shaderData = SerializableMaterialData.Build(material);
//                    var jsonData = JsonUtility.ToJson(shaderData, true);
//                    var nameHash = PackageHelper.GetStringHash(target.name);
//                    var outputPath = Path.Combine("Library", "BundleKitMetaData", $"{nameHash}.json");
//                    Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
//                    File.WriteAllText(outputPath, jsonData);
//                }
//            }
//        }
//    }
//}
