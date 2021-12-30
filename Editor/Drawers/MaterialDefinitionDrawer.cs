using BundleKit.Assets;
using BundleKit.Bundles;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static BundleKit.Utility.ShaderPicker;
using static UnityEditor.EditorGUI;

namespace BundleKit.Drawers
{
    [CustomPropertyDrawer(typeof(MaterialDefinition))]
    public class MaterialDefinitionDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            BeginProperty(position, label, property);
            var nameProp = property.FindPropertyRelative("name");
            var shaderProp = property.FindPropertyRelative("shader");

            var nameRect = new Rect(position.x, position.y, position.width / 2, position.height);
            var shaderRect = new Rect(position.x + (position.width / 2), position.y, position.width / 2, position.height);

            var ari = property.serializedObject.targetObject as AssetsReferenceImporter;
            if (!ari) base.OnGUI(position, property, label);

            var arb = AssetDatabase.LoadMainAssetAtPath(ari.assetPath) as AssetsReferenceBundle;
            if (!arb) base.OnGUI(position, property, label);


            var allShaderInfo = arb.Assets.OfType<Shader>().Select(s => new LocalShaderInfo(s.name, hasErrors: false, s.isSupported)).ToArray();
            BeginChangeCheck();
            var nameValue = TextField(nameRect, nameProp.stringValue);
            if (EndChangeCheck())
            {
                nameProp.stringValue = nameValue;
            }

            ShaderPopup(shaderRect, allShaderInfo, shaderProp.stringValue, OnSelectedShaderPopup);

            void OnSelectedShaderPopup(object shaderNameObj)
            {
                property.serializedObject.Update();
                string text = (string)shaderNameObj;
                if (!string.IsNullOrEmpty(text))
                {
                    Shader shader = Shader.Find(text);
                    if (shader != null)
                    {
                        var name = shaderProp.stringValue;
                        if (shader.name == name)
                            shaderProp.stringValue = string.Empty;
                        else
                            shaderProp.stringValue = shader.name;
                    }
                }
            }
            EndProperty();
        }
    }
}