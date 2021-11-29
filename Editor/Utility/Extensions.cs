using AssetsTools.NET;
using AssetsTools.NET.Extra;
using BundleKit.Assets;
using System.Collections.Generic;
using System.Linq;

namespace BundleKit.Utility
{
    public static class Extensions
    {
        public static bool IsNullOrEmpty<T>(this ICollection<T> collection)
        {
            return collection == null || collection.Count == 0;
        }

        public static AssetTypeValue GetValue(this AssetTypeValueField valueField, string fieldName)
        {
            return valueField.Get(fieldName).GetValue();
        }

        public static void SetValue(this AssetTypeValueField valueField, string fieldName, object value)
        {
            valueField.Get(fieldName).GetValue().Set(value);
        }
        public static void RemapPPtrs(this AssetTypeValueField field, AssetsFileInstance inst, IDictionary<(int fileId, long pathId), (int fileId, long pathId)> map)
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

                            //not a null pptr
                            if (pathId == 0) continue;
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
        public static void SetPPtrsFileId(this AssetTypeValueField field, AssetsFileInstance inst, int newFileId)
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
                            var pathId = child.Get("m_PathID").GetValue().AsInt64();

                            //not a null pptr
                            if (pathId == 0) continue;

                            fileIdField.Set(newFileId);
                        }
                        //recurse through dependencies
                        fieldStack.Push(child);
                    }
                }
            }
        }

        public static AssetID ConvertToAssetID(this AssetsFileInstance inst, int fileId, long pathId)
        {
            return new AssetID(ConvertToInstance(inst, fileId).path, pathId);
        }

        static AssetsFileInstance ConvertToInstance(AssetsFileInstance inst, int fileId)
        {
            if (fileId == 0)
                return inst;
            else
                return inst.dependencies[fileId - 1];
        }
        public static IEnumerable<AssetData> GetDependentAssetIds(this AssetsFileInstance inst, AssetsManager am, AssetTypeValueField field, int depth = 1, HashSet<AssetID> visitedAssetIds = null)
        {
            if (visitedAssetIds == null)
                visitedAssetIds = new HashSet<AssetID>();
            foreach (AssetTypeValueField child in field.children)
            {
                //not a value (ie not an int)
                if (!child.templateField.hasValue)
                {
                    //not array of values either
                    if (child.templateField.isArray && child.templateField.children[1].valueType != EnumValueTypes.ValueType_None)
                        continue;

                    string typeName = child.templateField.type;
                    //is a pptr
                    if (typeName.StartsWith("PPtr<") && typeName.EndsWith(">") /*&& child.childrenCount == 2*/)
                    {
                        int fileId = child.Get("m_FileID").GetValue().AsInt();
                        long pathId = child.Get("m_PathID").GetValue().AsInt64();

                        //not a null pptr
                        if (pathId == 0)
                            continue;

                        var assetId = inst.ConvertToAssetID(fileId, pathId);
                        //not already visited and not a monobehaviour
                        if (visitedAssetIds.Contains(assetId)) continue;
                        visitedAssetIds.Add(assetId);

                        var ext = am.GetExtAsset(inst, fileId, pathId);
                        var name = GetName(ext);

                        //we don't want to process monobehaviours as thats a project in itself
                        if (ext.info.curFileType == (int)AssetClassID.MonoBehaviour) continue;

                        yield return (ext, child, name, ext.file.name, fileId, pathId, depth);

                        //recurse through dependencies
                        foreach (var dep in GetDependentAssetIds(ext.file, am, ext.instance.GetBaseField(), depth + 1, visitedAssetIds))
                            yield return dep;
                    }
                    //recurse through dependencies
                    foreach (var dep in GetDependentAssetIds(inst, am, child, depth + 1, visitedAssetIds))
                        yield return dep;
                }
            }
        }
        public static string GetName(this AssetExternal asset)
        {
            return AssetHelper.GetAssetNameFastNaive(asset.file.file, asset.info);
            //switch ((AssetClassID)asset.info.curFileType)
            //{
            //    case AssetClassID.Shader:
            //        var parsedFormField = asset.instance.GetBaseField().Get("m_ParsedForm");
            //        var shaderNameField = parsedFormField.Get("m_Name");
            //        return shaderNameField.GetValue().AsString();
            //    default:
            //        var foundName = asset.info.ReadName(asset.file.file, out var name);
            //        if (foundName) return name;
            //        else return string.Empty;
            //}
        }
    }
}
