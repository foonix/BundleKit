using BundleKit.Bundles;
using UnityEditor;
using UnityEditor.Experimental.AssetImporters;
using UnityEngine;

namespace BundleKit.Editors
{
    [CustomEditor(typeof(SerializableMaterialDataImporter))]
    public class SerializableMaterialDataImporterEditor : ScriptedImporterEditor
    {
        SerializedMaterialDataEditor inspector;
        public override void OnEnable()
        {
            var assetPath = AssetDatabase.GetAssetPath(target);
            var mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            inspector = CreateEditor(mainAsset) as SerializedMaterialDataEditor;
        }
        public override void OnDisable()
        {
            inspector.draw = true;
            inspector = null;
        }
        protected override void OnHeaderGUI()
        {
            inspector.draw = true;
            inspector.DrawHeader();
        }
        public override void OnInspectorGUI()
        {
            inspector.OnInspectorGUI();
            inspector.draw = false;
        }
    }
}