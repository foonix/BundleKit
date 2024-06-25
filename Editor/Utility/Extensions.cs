using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
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

        public static AssetTree GetDependencies(
            this AssetsFileInstance inst, AssetsManager am, ResourceManagerDb resourceManagerDb,
            int fileId, long pathId, bool recurseFiles)
        {
            var fieldStack = new Stack<(AssetsFileInstance file, AssetTypeValueField field, AssetTree node)>();
            var baseAsset = am.GetExtAsset(inst, fileId, pathId);
            var baseField = baseAsset.baseField;

            resourceManagerDb.TryGetName(inst.name, pathId, out var rmName);
            var root = new AssetTree
            {
                name = baseAsset.GetName(am),
                resourceManagerName = rmName,
                sourceData = baseAsset,
                FileId = fileId,
                PathId = pathId,
                Children = new List<AssetTree>()
            };

            var first = (inst, baseField, root);
            fieldStack.Push(first);

            while (fieldStack.Any())
            {
                var current = fieldStack.Pop();
                foreach (var child in current.field.Children)
                {
                    //is a pptr
                    if (!child.TemplateField.IsArray && child.TryParsePPtr(am, current.file, out var pPtrDest))
                    {
                        if (!root.Children.Contains(pPtrDest) && root != pPtrDest)
                        {
                            root.Children.Add(pPtrDest);

                            // recurse through dependencies
                            if (((AssetClassID)pPtrDest.sourceData.info.TypeId).CanHaveDependencies() && recurseFiles)
                                fieldStack.Push((pPtrDest.sourceData.file, pPtrDest.sourceData.baseField, pPtrDest));
                        }

                        // Dependencies can be circular, so skip already visited dependencies.
                    }
                    else if (child.TemplateField.IsArray)
                    {
                        // is PPtr<T> array, eg m_Dependencies
                        if (child.TemplateField.Children[1].Type.StartsWith("PPtr<"))
                        {
                            foreach (var pPtr in child.Children)
                            {
                                if (pPtr.TryParsePPtr(am, current.file, out var pPtrArrayNode)
                                    && !root.Children.Contains(pPtrArrayNode) && root != pPtrArrayNode)
                                {
                                    root.Children.Add(pPtrArrayNode);
                                    if (((AssetClassID)pPtrArrayNode.sourceData.info.TypeId).CanHaveDependencies() && recurseFiles)
                                        fieldStack.Push((pPtrArrayNode.sourceData.file, pPtrArrayNode.sourceData.baseField, pPtrArrayNode));
                                }
                            }
                        }
                        else
                        {
                            // The array might be a struct type that contains a PPtr.
                            fieldStack.Push((current.file, child, current.node));
                        }
                    }
                    else
                        fieldStack.Push((current.file, child, current.node));
                }
            }

            return first.root;
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
            switch ((AssetClassID)asset.info.TypeId)
            {
                case AssetClassID.MonoBehaviour:
                    var nameField = asset.baseField["m_Name"];
                    return nameField.AsString;

                case AssetClassID.Shader:
                    var parsedFormField = asset.baseField["m_ParsedForm"];
                    var shaderNameField = parsedFormField["m_Name"];
                    return shaderNameField.AsString;
                default:
                    return AssetHelper.GetAssetNameFast(asset.file.file, am.ClassDatabase, asset.info);
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

        /// <summary>
        /// Returns false for some types known to be unable to have dependencies.  True otherwise.
        /// This is mainly to avoid loading and parsing large serialized objects.
        /// </summary>
        public static bool CanHaveDependencies(this AssetClassID classId) => classId switch
        {
            AssetClassID.Mesh or AssetClassID.Texture2D or AssetClassID.ComputeShader => false,
            _ => true,
        };

        /// <summary>
        /// Check if any filter's assetClass assetFileInfo's TypeId.
        ///
        /// This is a pre-check to avoid a more expensive name fetch.
        /// </summary>
        public static bool MatchesAnyClass(this Filter[] filters, AssetFileInfo assetFileInfo)
        {
            foreach (var filter in filters)
            {
                if ((AssetClassID)assetFileInfo.TypeId == filter.assetClass)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Test if the combination of AssetClassID and asset name matches any filter.
        /// </summary>
        public static bool AnyMatch(this Filter[] filters, AssetFileInfo assetFileInfo, string name)
        {
            if (name is null)
            {
                return false;
            }

            foreach (var filter in filters)
            {
                if (filter.Match(assetFileInfo, name))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
