using BundleKit.Bundles;
using UnityEditor;
using UnityEditor.Experimental.AssetImporters;

namespace BundleKit.Editors
{
    [CustomEditor(typeof(CatalogImporter))]
    public class CatalogImporterEditor : Editor
    {
        //public override bool showImportedObject => true;

        protected override void OnHeaderGUI()
        {

        }
        public override void OnInspectorGUI()
        {
        }
    }
}