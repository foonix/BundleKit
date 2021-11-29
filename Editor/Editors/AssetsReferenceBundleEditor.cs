using BundleKit.Bundles;
using UnityEditor;
using static UnityEditor.EditorGUI;

namespace BundleKit.Editors
{
    [CustomEditor(typeof(AssetsReferenceBundle))]
    public class AssetsReferenceBundleEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            //DrawPropertiesExcluding(serializedObject, "m_Script", nameof(AssetsReferenceBundle.Assets));
            using (new DisabledScope(true))
            {
                DrawPropertiesExcluding(serializedObject, "m_Script"/*, nameof(AssetsReferenceBundle.CustomMaterials)*/);
            }
        }
    }
}