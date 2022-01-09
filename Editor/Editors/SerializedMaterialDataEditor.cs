using BundleKit.Assets;
using BundleKit.Bundles;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using static BundleKit.Utility.ShaderPicker;

namespace BundleKit.Editors
{
    [CustomEditor(typeof(Material))]
    public class SerializedMaterialDataEditor : MaterialEditor
    {
        public bool draw = false;
        protected override void OnHeaderGUI()
        {
            var assetPath = AssetDatabase.GetAssetPath(target);
            var importer = AssetImporter.GetAtPath(assetPath);
            var material = target as Material;
            if (importer is SerializableMaterialDataImporter smdi)
            {
                if (!draw) return;

                var allShaderInfo = AssetBundle.GetAllLoadedAssetBundles()
                    .SelectMany(bundle => bundle.LoadAllAssets<Shader>())
                    .Select(s => new LocalShaderInfo(s.name, hasErrors: false, s.isSupported))
                    .ToArray();


                GUI.Box(new Rect(0, 0, EditorGUIUtility.currentViewWidth, 46), new GUIContent(), BaseStyles.inspectorBig);
                var cursor = EditorGUILayout.GetControlRect();
                cursor = new Rect(cursor.x, cursor.y + 4, cursor.width, cursor.height);
                OnPreviewGUI(new Rect(cursor.x + 2, cursor.y + 2, 32, 32), BaseStyles.inspectorBigInner);

                cursor = new Rect(cursor.x + 40, cursor.y, cursor.width, cursor.height);
                GUI.Label(cursor, target.name, EditorStyles.largeLabel);


                var shaderLabelContent = new GUIContent("Shader");
                var offset = GUIStyle.none.CalcSize(shaderLabelContent).x;
                cursor = new Rect(cursor.x + 2, cursor.y + 22, offset - 2, cursor.height);
                GUI.Label(cursor, shaderLabelContent, GUIStyle.none);

                cursor = new Rect(cursor.x + offset + 7, cursor.y - 1, EditorGUIUtility.currentViewWidth - (cursor.width + cursor.x) - 16, cursor.height);
                ShaderPopup(cursor, allShaderInfo, material.shader.name, OnSelectedShaderPopup);


                cursor = new Rect(cursor.x - offset - 50, cursor.y + 5, 32, cursor.height);
                

                GUILayout.Space(32);

                void OnSelectedShaderPopup(object shaderNameObj)
                {
                    var serializedObject = new SerializedObject(smdi);
                    serializedObject.Update();
                    string text = (string)shaderNameObj;
                    if (!string.IsNullOrEmpty(text))
                    {
                        Shader shader = Shader.Find(text);
                        if (shader != null)
                        {
                            var shaderProp = serializedObject.FindProperty("shader");
                            var name = shaderProp.stringValue;
                            if (shader.name == name)
                                shaderProp.stringValue = string.Empty;
                            else
                                shaderProp.stringValue = shader.name;

                            material.shader = shader;
                            WriteChanges();
                        }
                    }
                }
                return;
            }
            else
                base.OnHeaderGUI();
        }
        public override void OnInspectorGUI()
        {
            var assetPath = AssetDatabase.GetAssetPath(target);
            var smdi = AssetImporter.GetAtPath(assetPath) as SerializableMaterialDataImporter;
            if (smdi)
            {
                if (!draw) return;
                target.hideFlags = HideFlags.None;
                EditorGUI.BeginChangeCheck();
            }
            base.OnInspectorGUI();
            if (smdi && EditorGUI.EndChangeCheck())
            {
                WriteChanges();
            }
        }

        private void WriteChanges()
        {
            var assetPath = AssetDatabase.GetAssetPath(target);
            var material = target as Material;
            var smd = JsonUtility.FromJson<SerializableMaterialData>(File.ReadAllText(assetPath));
            var shaderData = SerializableMaterialData.Build(material, smd.identity);
            var jsonData = JsonUtility.ToJson(shaderData, true);
            File.WriteAllText(assetPath, jsonData);
        }
        private static class BaseStyles
        {
            public static readonly GUIContent open;

            public static readonly GUIStyle inspectorBig;

            public static readonly GUIStyle inspectorBigInner;

            public static readonly GUIStyle centerStyle;

            public static readonly GUIStyle postLargeHeaderBackground;

            static BaseStyles()
            {
                open = EditorGUIUtility.TrTextContent("Open");
                inspectorBig = new GUIStyle(GetStyle("In BigTitle"));
                inspectorBigInner = "IN BigTitle inner";
                postLargeHeaderBackground = "IN BigTitle Post";
                centerStyle = new GUIStyle();
                centerStyle.alignment = TextAnchor.MiddleCenter;
            }
            static GUIStyle GetStyle(string styleName)
            {
                GUIStyle gUIStyle = GUI.skin.FindStyle(styleName) ?? EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).FindStyle(styleName);
                if (gUIStyle == null)
                {
                    Debug.LogError("Missing built-in guistyle " + styleName);
                    gUIStyle = new GUIStyle();
                    gUIStyle.name = "StyleNotFoundError";
                }

                return gUIStyle;
            }
        }
    }
}
