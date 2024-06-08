﻿using BundleKit.Assets;
using BundleKit.Bundles;
using UnityEditor;
#if UNITY_2020_2_OR_NEWER
using UnityEditor.AssetImporters;
#else
using UnityEditor.Experimental.AssetImporters;
#endif

using UnityEditorInternal;
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
            InternalEditorUtility.SetIsInspectorExpanded(mainAsset, true);
        }
        public override void OnDisable()
        {
            if (!inspector) return;
            inspector.draw = true;
            inspector = null;
        }
        protected override void OnHeaderGUI()
        {
            if (!inspector) return;
            inspector.draw = true;
            inspector.DrawHeader();
        }
        public override void OnInspectorGUI()
        {
            if (!inspector) return;
            inspector.OnInspectorGUI();
            inspector.draw = false;
        }
    }
}