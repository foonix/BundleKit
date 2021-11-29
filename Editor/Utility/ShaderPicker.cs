using BundleKit.Assets;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static UnityEditor.EditorGUI;

namespace BundleKit.Utility
{
    public static class ShaderPicker
    {
        public static void ShaderPopup(Rect controlRect, LocalShaderInfo[] allShaderInfo, string selectedName, GenericMenu.MenuFunction2 onSelectedShader)
        {
            bool enabled = GUI.enabled;
            GUIContent content = EditorGUIUtility.TrTempContent((!(selectedName != null)) ? "No Shader Selected" : selectedName);
            if (DropdownButton(controlRect, content, FocusType.Keyboard, "MiniPulldown"))
            {
                GenericMenu genericMenu = new GenericMenu();
                CreateShaderList(genericMenu, allShaderInfo, selectedName, onSelectedShader);
                genericMenu.DropDown(controlRect);
            }
            GUI.enabled = enabled;
        }

        private static void CreateShaderList(GenericMenu menu, LocalShaderInfo[] allShaderInfo, string selectedName, GenericMenu.MenuFunction2 onSelectedShader)
        {
            List<string> list = new List<string>();
            List<string> list2 = new List<string>();
            List<string> list3 = new List<string>();
            List<string> list4 = new List<string>();
            List<string> list5 = new List<string>();
            var array = allShaderInfo;
            for (int i = 0; i < array.Length; i++)
            {
                var shaderInfo = array[i];
                if (!shaderInfo.name.StartsWith("Deprecated") && !shaderInfo.name.StartsWith("Hidden"))
                {
                    if (shaderInfo.hasErrors)
                    {
                        list5.Add(shaderInfo.name);
                    }
                    else if (!shaderInfo.supported)
                    {
                        list4.Add(shaderInfo.name);
                    }
                    else if (shaderInfo.name.StartsWith("Legacy Shaders/"))
                    {
                        list3.Add(shaderInfo.name);
                    }
                    else if (shaderInfo.name.Contains("/"))
                    {
                        list2.Add(shaderInfo.name);
                    }
                    else
                    {
                        list.Add(shaderInfo.name);
                    }
                }
            }

            list.Sort();
            list2.Sort();
            list3.Sort();
            list4.Sort();
            list5.Sort();
            list.ForEach(delegate (string s)
            {
                AddShaderToMenu("", menu, s, selectedName == s, onSelectedShader);
            });
            list2.ForEach(delegate (string s)
            {
                AddShaderToMenu("", menu, s, selectedName == s, onSelectedShader);
            });
            if (list3.Any())
            {
                menu.AddSeparator("");
            }

            list3.ForEach(delegate (string s)
            {
                AddShaderToMenu("", menu, s, selectedName == s, onSelectedShader);
            });
            if (list4.Any())
            {
                menu.AddSeparator("");
            }

            list4.ForEach(delegate (string s)
            {
                AddShaderToMenu("Not supported/", menu, s, selectedName == s, onSelectedShader);
            });
            if (list5.Any())
            {
                menu.AddSeparator("");
            }

            list5.ForEach(delegate (string s)
            {
                AddShaderToMenu("Failed to compile/", menu, s, false, onSelectedShader);
            });
        }
        private static void AddShaderToMenu(string prefix, GenericMenu menu, string shaderName, bool on, GenericMenu.MenuFunction2 onSelectedShader)
        {
            menu.AddItem(new GUIContent(prefix + shaderName), on, onSelectedShader, shaderName);
        }
    }
}
