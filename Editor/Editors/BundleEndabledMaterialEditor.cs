using BundleKit.Bundles;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using static UnityEditor.EditorGUI;

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
            if (mainAsset is AssetsReferenceBundle arb)
            {
                if (arb.CustomMaterials.Contains(target))
                {
                    target.hideFlags = HideFlags.None;
                }
            }
            base.OnInspectorGUI();
        }
    }
}