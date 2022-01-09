using AssetsTools.NET;
using AssetsTools.NET.Extra;
using BundleKit.Assets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor.Build.Content;

namespace BundleKit.Utility
{
    public delegate void UpdateLog(string title = null, string message = null, float progress = -1, bool log = true, params string[] context);

    public static class Extensions
    {
        public const string unityBuiltinExtra = "Resources/unity_builtin_extra";
        public const string unityDefaultResources = "Resources/unity default resources";

        public static UnityEngine.BuildCompression AsBuildCompression(this Compression compression)
        {
            switch (compression)
            {
                case Compression.Uncompressed:
                    return UnityEngine.BuildCompression.Uncompressed;
                case Compression.LZMA:
                    return UnityEngine.BuildCompression.LZMA;
                case Compression.LZ4:
                    return UnityEngine.BuildCompression.LZ4;
            }

            throw new NotSupportedException();
        }
        public static bool IsNullOrEmpty<T>(this ICollection<T> collection) => collection == null || collection.Count == 0;

        public static AssetTree GetHierarchy(this AssetsFileInstance inst, AssetsManager am, int fileId, long pathId)
        {
            var fieldStack = new Stack<(AssetsFileInstance file, AssetTypeValueField field, AssetTree node)>();
            var baseAsset = am.GetExtAsset(inst, fileId, pathId);
            var baseField = baseAsset.instance.GetBaseField();

            var root = new AssetTree
            {
                name = baseAsset.GetName(am),
                assetExternal = baseAsset,
                FileId = fileId,
                PathId = pathId,
                Children = new List<AssetTree>()
            };

            var first = (inst, baseField, root);
            fieldStack.Push(first);

            while (fieldStack.Any())
            {
                var current = fieldStack.Pop();
                foreach (var child in current.field.children)
                {
                    //not a value (ie not an int)
                    if (!child.templateField.hasValue)
                    {
                        //not array of values either
                        if (child.templateField.isArray && child.templateField.children[1].valueType != EnumValueTypes.ValueType_None)
                            continue;

                        string typeName = child.templateField.type;
                        //is a pptr
                        if (typeName.StartsWith("PPtr<") && typeName.EndsWith(">"))
                        {
                            var pathIdRef = child.Get("m_PathID").GetValue().AsInt64();
                            if (pathIdRef == 0)
                                continue;

                            var fileIdRef = child.Get("m_FileID").GetValue().AsInt();
                            var ext = am.GetExtAsset(current.file, fileIdRef, pathIdRef);

                            //we don't want to process monobehaviours as thats a project in itself
                            if (ext.info.curFileType == (int)AssetClassID.MonoBehaviour)
                                continue;

                            var node = new AssetTree
                            {
                                name = ext.GetName(am),
                                assetExternal = ext,
                                FileId = fileIdRef,
                                PathId = pathIdRef,
                                Children = new List<AssetTree>()
                            };
                            current.node.Children.Add(node);

                            //recurse through dependencies
                            fieldStack.Push((ext.file, ext.instance.GetBaseField(), node));
                        }
                        else
                            fieldStack.Push((current.file, child, current.node));
                    }
                }
            }

            return first.root;
        }

        public static IEnumerable<AssetTree> CollectAssetTrees(this AssetsFileInstance assetsFileInst, AssetsManager am, Regex[] nameRegex, AssetClassID assetClass, UpdateLog Update)
        {
            // Iterate over all requested Class types and collect the data required to copy over the required asset information
            // This step will recurse over dependencies so all required assets will become available from the resulting bundle
            Update("Collecting Asset Trees", log: false);
            var fileInfos = assetsFileInst.table.GetAssetsOfType((int)assetClass);
            for (var x = 0; x < fileInfos.Count; x++)
            {
                var assetFileInfo = fileInfos[x];

                var name = AssetHelper.GetAssetNameFast(assetsFileInst.file, am.classFile, assetFileInfo);
                Update("Collecting Asset Trees", $"({assetClass}) {name}", log: true);

                // If a name Regex filter is applied, and it does not match, continue
                int i = 0;
                for (; i < nameRegex.Length; i++)
                    if (nameRegex[i] != null && nameRegex[i].IsMatch(name))
                        break;
                if (nameRegex.Length != 0 && i == nameRegex.Length) continue;

                yield return assetsFileInst.GetHierarchy(am, 0, assetFileInfo.index);
            }
        }

        public static void ImportTextureData(this TextureFile texFile, Dictionary<string, Stream> streamReaders, string dataDirectoryPath)
        {
            var streamPath = texFile.m_StreamData.path;
            var newPath = Path.Combine(dataDirectoryPath, streamPath);
            var fixedNewPath = newPath.Replace("\\", "/");
            try
            {
                var offset = texFile.m_StreamData.offset;
                var size = texFile.m_StreamData.size;

                Stream stream;
                if (streamReaders.ContainsKey(fixedNewPath))
                    stream = streamReaders[fixedNewPath];
                else
                    streamReaders[fixedNewPath] = stream = File.OpenRead(fixedNewPath);

                texFile.pictureData = new byte[texFile.m_StreamData.size];
                stream.Position = (long)texFile.m_StreamData.offset;
                stream.Read(texFile.pictureData, 0, (int)texFile.m_StreamData.size);

                texFile.m_StreamData.offset = 0;
                texFile.m_StreamData.size = 0;
                texFile.m_StreamData.path = string.Empty;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError(e);
            }
        }

        public static string GetName(this AssetExternal asset, AssetsManager am)
        {
            switch ((AssetClassID)asset.info.curFileType)
            {
                case AssetClassID.MonoBehaviour:
                    var nameField = asset.instance.GetBaseField().Get("m_Name");
                    return nameField.GetValue().AsString();

                case AssetClassID.Shader:
                    var parsedFormField = asset.instance.GetBaseField().Get("m_ParsedForm");
                    var shaderNameField = parsedFormField.Get("m_Name");
                    return shaderNameField.GetValue().AsString();
                default:
                    return AssetHelper.GetAssetNameFast(asset.file.file, am.classFile, asset.info);
            }
        }

        [Serializable]
        public struct ObjectTypes
        {
            public ObjectIdentifier ObjectID;

            public Type[] Types;

            public ObjectTypes(ObjectIdentifier objectID, Type[] types)
            {
                ObjectID = objectID;
                Types = types;
            }
        }
    }

}
