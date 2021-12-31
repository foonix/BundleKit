using AssetsTools.NET;
using AssetsTools.NET.Extra;
using BundleKit.Assets;
using BundleKit.Building;
using BundleKit.PipelineJobs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Utilities;

namespace BundleKit.Utility
{
    internal static class TrackerExtensions
    {
        public static string HumanReadable(this string camelCased)
        {
            return Regex.Replace(camelCased, "(\\B[A-Z]+?(?=[A-Z][^A-Z])|\\B[A-Z]+?(?=[^A-Z]))", " $1");
        }

        public static bool UpdateTaskUnchecked(this IProgressTracker tracker, string taskTitle)
        {
            return tracker?.UpdateTask(taskTitle) ?? true;
        }

        public static bool UpdateInfoUnchecked(this IProgressTracker tracker, string taskInfo)
        {
            return tracker?.UpdateInfo(taskInfo) ?? true;
        }
    }

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


        public static IEnumerable<AssetTypeValueField> FindFieldType(this AssetTypeValueField valueField, Predicate<string> typeMatch)
        {
            var fieldStack = new Stack<AssetTypeValueField>();
            AssetTypeValueField field = null;
            fieldStack.Push(valueField);
            while (fieldStack.Any())
            {
                field = fieldStack.Pop();
                if (field.childrenCount > 0)
                {
                    string typeName = field.templateField.type;
                    if (typeMatch(typeName))
                    {
                        yield return field;
                    }
                    foreach (var child in field.children)
                        fieldStack.Push(child);
                }
            }
        }
        public static IEnumerable<AssetTypeValueField> FindField(this AssetTypeValueField valueField, string fieldPath)
        {
            var fieldStack = new Stack<AssetTypeValueField>();
            AssetTypeValueField field = null;
            fieldStack.Push(valueField);
            while (fieldStack.Any())
            {
                field = fieldStack.Pop();
                if (field.childrenCount > 0)
                {
                    var targetField = field.Get(fieldPath);
                    if (targetField.childrenCount > -1)
                    {
                        yield return targetField;
                    }
                    foreach (var child in field.children)
                        fieldStack.Push(child);
                }
            }
        }

        public static AssetTypeValueField Get(this AssetTypeValueField valueField, params string[] fieldPath)
        {
            var field = valueField;
            foreach (var pathField in fieldPath)
                field = field.Get(pathField);
            return field;
        }

        public static AssetTypeValueField GetField(this AssetTypeValueField valueField, string fieldPath) => valueField.Get(fieldPath.Split('/'));

        public static AssetTypeValue GetValue(this AssetTypeValueField valueField, string fieldName) => valueField.Get(fieldName.Split('/')).GetValue();

        public static void SetValue(this AssetTypeValueField valueField, string fieldName, object value) => valueField.GetField(fieldName).GetValue().Set(value);

        public static void RemapPPtrs(this AssetTypeValueField field, IDictionary<(int fileId, long pathId), (int fileId, long pathId)> map)
        {
            var fieldStack = new Stack<AssetTypeValueField>();
            fieldStack.Push(field);
            while (fieldStack.Any())
            {
                var current = fieldStack.Pop();
                foreach (AssetTypeValueField child in current.children)
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
                            var fileIdField = child.Get("m_FileID").GetValue();
                            var pathIdField = child.Get("m_PathID").GetValue();
                            var pathId = pathIdField.AsInt64();
                            var fileId = fileIdField.AsInt();
                            if (!map.ContainsKey((fileId, pathId))) continue;

                            var newPPtr = map[(fileId, pathId)];
                            fileIdField.Set(newPPtr.fileId);
                            pathIdField.Set(newPPtr.pathId);
                        }
                        //recurse through dependencies
                        fieldStack.Push(child);
                    }
                }
            }

        }


        public static AssetTypeValueField CreateEntry(this AssetTypeValueField containerArray, string name, int fileId, long pathId, int preloadIndex = 0, int preloadSize = 0)
        {
            var pair = ValueBuilder.DefaultValueFieldFromArrayTemplate(containerArray);
            //Name the asset identified by this element
            pair.Get("first").GetValue().Set(name);

            //Get fields for populating index and 
            var second = pair.Get("second");
            var assetField = second.Get("asset");

            //We are not constructing a preload table, so we are setting these all to zero
            second.SetValue("preloadIndex", preloadIndex);
            second.SetValue("preloadSize", preloadSize);

            // Update the fileId and PathID so that asset can be located within the bundle
            // We zero out the fileId because the asset is in the local file, not a dependent file
            assetField.SetValue("m_FileID", fileId);
            assetField.SetValue("m_PathID", pathId);
            return pair;
        }
        public static AssetID ConvertToAssetID(this AssetsFileInstance inst, int fileId, long pathId)
        {
            var nextInst = ConvertToInstance(inst, fileId);
            if (nextInst == null) return null;

            return new AssetID(nextInst.path, pathId);
        }

        static AssetsFileInstance ConvertToInstance(AssetsFileInstance inst, int fileId)
        {
            if (fileId == 0)
                return inst;
            else
                return inst.dependencies[fileId - 1];
        }

        public static IEnumerable<AssetData> GetDependentAssetIds(this AssetsFileInstance inst, HashSet<AssetID> visited, AssetTypeValueField field, AssetsManager am, UpdateLog Update, bool crossFiles)
        {
            var fieldStack = new Stack<(AssetsFileInstance inst, AssetTypeValueField field, int depth)>();
            fieldStack.Push((inst, field, depth: 0));
            long p = 0;
            float modVal = 100f;
            while (fieldStack.Any())
            {
                var set = fieldStack.Pop();
                var current = set.field;
                var currentInst = set.inst;
                var depth = set.depth;

                foreach (var child in current.children)
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
                            var pathId = child.Get("m_PathID").GetValue().AsInt64();
                            if (pathId == 0)
                                continue;

                            var fileId = child.Get("m_FileID").GetValue().AsInt();
                            if (!crossFiles && fileId > 0)
                                continue;

                            var assetId = currentInst.ConvertToAssetID(fileId, pathId);
                            if (assetId == null || visited.Contains(assetId))
                                continue;

                            visited.Add(assetId);
                            var ext = am.GetExtAsset(currentInst, fileId, pathId);
                            var name = ext.GetName(am);
                            Update(null, $"({(AssetClassID)ext.info.curFileType}) {name}", ((++p) % modVal) / modVal, false);

                            //we don't want to process monobehaviours as thats a project in itself
                            if (ext.info.curFileType == (int)AssetClassID.MonoBehaviour)
                                continue;

                            yield return (ext, name, ext.file.name, 0, pathId, depth);

                            fieldStack.Push((ext.file, ext.instance.GetBaseField(), depth + 1));
                        }
                        else
                            //recurse through dependencies
                            fieldStack.Push((currentInst, child, depth));
                    }
                }
            }

        }

        [DebuggerDisplay("{assetsFileInstance.name}/{name} fid:{FileId} pid:{PathId} children: {Children.Count}")]
        public struct AssetTree : IEquatable<AssetTree>
        {
            public string name;
            public AssetExternal assetExternal;
            public int FileId;
            public long PathId;
            public List<AssetTree> Children;

            public IEnumerable<(int fileId, long pathId)> FlattenIds(bool enterDependencies)
            {
                yield return (FileId, PathId);
                foreach (var child in Children)
                    if (enterDependencies || child.FileId == 0)
                        foreach (var result in child.FlattenIds(enterDependencies))
                            yield return result;
            }
            public IEnumerable<AssetTree> Flatten(bool enterDependencies)
            {
                yield return this;
                foreach (var child in Children)
                    if (enterDependencies || child.FileId == 0)
                        foreach (var result in child.Flatten(enterDependencies))
                            yield return result;
            }

            public override bool Equals(object obj)
            {
                return obj is AssetTree tree && Equals(tree);
            }

            public bool Equals(AssetTree other)
            {
                return EqualityComparer<AssetsFileInstance>.Default.Equals(assetExternal.file, other.assetExternal.file) &&
                       PathId == other.PathId;
            }

            public override int GetHashCode()
            {
                int hashCode = -1120199924;
                hashCode = hashCode * -1521134295 + EqualityComparer<AssetsFileInstance>.Default.GetHashCode(assetExternal.file);
                hashCode = hashCode * -1521134295 + PathId.GetHashCode();
                return hashCode;
            }

            public static bool operator ==(AssetTree left, AssetTree right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(AssetTree left, AssetTree right)
            {
                return !(left == right);
            }
        }

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

        public static void ImportStreamData(this AssetTypeValueField baseField, AssetTypeValueField streamData, Dictionary<string, Stream> streamReaders, string dataDirectoryPath, bool updatePath = false)
        {
            if (streamData?.children != null)
            {

                var path = streamData.Get("path");
                var streamPath = path.GetValue().AsString();
                var newPath = Path.Combine(dataDirectoryPath, streamPath);
                var fixedNewPath = newPath/*.Replace("\\", "/")*/;
                try
                {
                    if (updatePath) streamData.SetValue("path", fixedNewPath);
                    else
                    {
                        var offset = streamData.GetValue("offset").AsInt64();
                        var size = streamData.GetValue("size").AsInt();
                        var data = new byte[size];

                        Stream stream;
                        if (streamReaders.ContainsKey(fixedNewPath))
                            stream = streamReaders[fixedNewPath];
                        else
                            streamReaders[fixedNewPath] = stream = File.OpenRead(fixedNewPath);

                        stream.Position = offset;
                        stream.Read(data, 0, (int)size);

                        if (data != null && data.Length > 0)
                        {
                            streamData.SetValue("offset", 0);
                            streamData.SetValue("size", 0);
                            streamData.SetValue("path", string.Empty);
                            var image_data = baseField.GetField("image data");
                            image_data.GetValue().type = EnumValueTypes.ByteArray;
                            image_data.templateField.valueType = EnumValueTypes.ByteArray;
                            var byteArray = new AssetTypeByteArray()
                            {
                                size = (uint)data.Length,
                                data = data
                            };
                            image_data.GetValue().Set(byteArray);
                            baseField.Get("m_CompleteImageSize").GetValue().Set(data.Length);
                        }
                    }
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError(e);
                }
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

        public static void GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, out TValue value) where TValue : new()
        {
            if (!dictionary.TryGetValue(key, out value))
            {
                value = new TValue();
                dictionary.Add(key, value);
            }
        }

        public static void Swap<T>(this IList<T> list, int first, int second)
        {
            T value = list[second];
            list[second] = list[first];
            list[first] = value;
        }

        public static void ExtractCommonCacheData(IBuildCache cache, IEnumerable<ObjectIdentifier> includedObjects, IEnumerable<ObjectIdentifier> referencedObjects, HashSet<Type> uniqueTypes, List<ObjectTypes> objectTypes, HashSet<CacheEntry> dependencies)
        {
            if (includedObjects != null)
            {
                foreach (ObjectIdentifier includedObject in includedObjects)
                {
                    Type[] sortedUniqueTypesForObject = BuildCacheUtility.GetSortedUniqueTypesForObject(includedObject);
                    objectTypes.Add(new ObjectTypes(includedObject, sortedUniqueTypesForObject));
                    uniqueTypes.UnionWith(sortedUniqueTypesForObject);
                }
            }

            if (referencedObjects != null)
            {
                foreach (ObjectIdentifier referencedObject in referencedObjects)
                {
                    Type[] sortedUniqueTypesForObject2 = BuildCacheUtility.GetSortedUniqueTypesForObject(referencedObject);
                    objectTypes.Add(new ObjectTypes(referencedObject, sortedUniqueTypesForObject2));
                    uniqueTypes.UnionWith(sortedUniqueTypesForObject2);
                    dependencies.Add(cache.GetCacheEntry(referencedObject));
                }
            }

            dependencies.UnionWith(uniqueTypes.Where(t => t != null).Select(cache.GetCacheEntry));
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
