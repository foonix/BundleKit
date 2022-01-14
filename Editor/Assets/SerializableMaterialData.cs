using BundleKit.Bundles;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static UnityEditor.ShaderUtil;

namespace BundleKit.Assets
{
    [System.Serializable]
    public struct SerializableMaterialData
    {
        public string identity;
        public string name;
        public string shader;
        public bool doubleSidedGI;
        public bool enableInstancing;
        public int renderQueue;
        public MaterialGlobalIlluminationFlags globalIlluminationFlags;
        public string[] shaderKeywords;
        public SerializableShaderProperty[] properties;

        public static SerializableMaterialData Build(Material material, string identity = null)
        {
            var serializedMaterial = new SerializableMaterialData();
            var shader = material.shader;
            serializedMaterial.name = material.name;
            serializedMaterial.shader = shader.name;

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
                        {
                            var asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                            if (asset is Catalog catalog)
                            {
                                try
                                {
                                    for (int j = 0; j < catalog.Assets.Count; j++)
                                    {
                                        var catalogAsset = catalog.Assets[j];
                                        if (textEnv.name == catalogAsset.internalAsset.name)
                                        {
                                            localId = catalogAsset.sourceId;
                                            break;
                                        }
                                    }
                                }
                                finally { }
                            }
                            data.SetValue(new TextureReference
                            {
                                guid = guid,
                                localId = localId,
                                offset = offset,
                                scale = scale
                            });
                        }
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
            serializedMaterial.identity = identity ?? GUID.Generate().ToString();
            return serializedMaterial;
        }

        public Material ToMaterial()
        {
            Shader shaderObj = Shader.Find(shader);
            Material material = new Material(shaderObj);
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
                        try
                        {
                            var textureReference = data.textureReference;
                            var path = AssetDatabase.GUIDToAssetPath(textureReference.guid);
                            var mainAsset = AssetDatabase.LoadMainAssetAtPath(path);
                            Texture texture = null;
                            switch (mainAsset)
                            {
                                case Texture tex:
                                    texture = tex;
                                    break;
                                case Catalog catalog:
                                    var localId = textureReference.localId;
                                    var assetMap = catalog.Assets.FirstOrDefault(entry => entry.sourceId == localId);
                                    texture = assetMap.externalAsset as Texture;
                                    break;
                            }
                            if (texture)
                            {
                                material.SetTexture(data.name, texture);
                                material.SetTextureOffset(data.name, textureReference.offset);
                                material.SetTextureScale(data.name, textureReference.scale);
                            }
                        }
                        catch { }
                        break;
                    default:
                        throw new InvalidOperationException($"Property: {data.name} has unsupported type: {data.type}");
                }
            }
            return material;
        }
    }
}

