using BundleKit.Bundles;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static UnityEditor.ShaderUtil;

namespace BundleKit.Assets
{

    [Serializable]
    struct SerializableMaterialData
    {
        public string name;
        public SerializableShaderProperty[] properties;
        public bool doubleSidedGI;
        public bool enableInstancing;
        public int renderQueue;
        public string[] shaderKeywords;
        public MaterialGlobalIlluminationFlags globalIlluminationFlags;

        public static SerializableMaterialData Build(Material material)
        {
            var serializedMaterial = new SerializableMaterialData();
            var shader = material.shader;
            serializedMaterial.name = material.name;

            var dataList = new List<SerializableShaderProperty>();
            for (int i = 0; i < GetPropertyCount(shader); i++)
            {
                var data = new SerializableShaderProperty
                {
                    name = GetPropertyName(shader, i),
                    type = GetPropertyType(shader, i)
                };
                switch (data.type)
                {
                    case ShaderPropertyType.Color:
                        data.SetValue(material.GetColor(data.name));
                        break;
                    case ShaderPropertyType.Float:
                        data.SetValue(material.GetFloat(data.name));
                        break;
                    case ShaderPropertyType.Vector:
                        data.SetValue(material.GetVector(data.name));
                        break;
                    case ShaderPropertyType.Range:
                        data.SetValue(material.GetFloat(data.name));
                        break;
                    case ShaderPropertyType.TexEnv:
                        var offset = material.GetTextureOffset(data.name);
                        var scale = material.GetTextureScale(data.name);
                        var textEnv = material.GetTexture(data.name);
                        if (textEnv && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(textEnv, out string guid, out long localId))
                            data.SetValue(new TextureReference
                            {
                                guid = guid,
                                localId = localId,
                                offset = offset,
                                scale = scale
                            });
                        break;
                    default:
                        throw new InvalidOperationException($"Property: {data.name} has unsupported type: {data.type}");
                }
                dataList.Add(data);
            }

            serializedMaterial.properties = dataList.ToArray();
            serializedMaterial.doubleSidedGI = material.doubleSidedGI;
            serializedMaterial.enableInstancing = material.enableInstancing;
            serializedMaterial.renderQueue = material.renderQueue;
            serializedMaterial.shaderKeywords = material.shaderKeywords;
            serializedMaterial.globalIlluminationFlags = material.globalIlluminationFlags;
            return serializedMaterial;
        }

        public void Apply(Material material, Dictionary<long, Texture> localTextures)
        {
            material.doubleSidedGI = doubleSidedGI;
            material.enableInstancing = enableInstancing;
            material.renderQueue = renderQueue;
            material.shaderKeywords = shaderKeywords;
            material.globalIlluminationFlags = globalIlluminationFlags;
            foreach (var data in properties)
            {
                switch (data.type)
                {
                    case ShaderPropertyType.Color:
                        material.SetColor(data.name, data.colorValue);
                        break;
                    case ShaderPropertyType.Float:
                        material.SetFloat(data.name, data.floatValue);
                        break;
                    case ShaderPropertyType.Vector:
                        material.SetVector(data.name, data.vectorValue);
                        break;
                    case ShaderPropertyType.Range:
                        material.SetFloat(data.name, data.floatValue);
                        break;
                    case ShaderPropertyType.TexEnv:
                        var textureReference = data.textureReference;
                        var path = AssetDatabase.GUIDToAssetPath(textureReference.guid);
                        var texture = AssetDatabase.LoadAssetAtPath<Texture>(path);
                        if (!texture && localTextures.ContainsKey(textureReference.localId))
                            texture = localTextures[textureReference.localId];
                        if (texture)
                        {
                            material.SetTexture(data.name, texture);
                            material.SetTextureOffset(data.name, textureReference.offset);
                            material.SetTextureScale(data.name, textureReference.scale);
                        }
                        break;
                    default:
                        throw new InvalidOperationException($"Property: {data.name} has unsupported type: {data.type}");
                }

            }
        }
    }
}

