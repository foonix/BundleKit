using BundleKit.Assets;
using BundleKit.Bundles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ThunderKit.Common.Package;
using UnityEditor;
using UnityEngine;

namespace BundleKit.Editors
{
    [CustomEditor(typeof(Material))]
    public class BundleEndabledMaterialEditor : MaterialEditor
    {
        protected override void OnHeaderGUI()
        {
            var mainAsset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GetAssetPath(target));
            if (mainAsset is AssetsReferenceBundle arb)
            {
                if (arb.CustomMaterials.Contains(target))
                {
                    //using (new DisabledScope(true))
                    base.OnHeaderGUI();

                    return;
                }
            }
            base.OnHeaderGUI();
        }
        public override void OnInspectorGUI()
        {
            var mainAsset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GetAssetPath(target));
            var arb = mainAsset as AssetsReferenceBundle;
            if (arb)
            {
                if (arb.CustomMaterials.Contains(target))
                {
                    target.hideFlags = HideFlags.None;
                }
            }
            base.OnInspectorGUI();
            if (arb)
            {
                if (arb.CustomMaterials.Contains(target))
                {
                    var material = target as Material;
                    var shaderData = SerializableMaterialData.Build(material);
                    var jsonData = JsonUtility.ToJson(shaderData, true);
                    var nameHash = PackageHelper.GetStringHash(target.name);
                    var outputPath = Path.Combine("Library", "BundleKitMetaData", $"{nameHash}.json");
                    Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                    File.WriteAllText(outputPath, jsonData);
                }
            }
        }
    }
}