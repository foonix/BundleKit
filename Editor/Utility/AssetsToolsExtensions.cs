using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.Build.Pipeline.Utilities;

namespace BundleKit.Utility
{
    public static class AssetsToolsExtensions
    {
        public static (BundleFileInstance bun, AssetsFileInstance bundleAssetsFile, AssetExternal assetBundleExtAsset) LoadBundle(this AssetsManager am, string path)
        {
            //Load bundle file and its AssetsFile
            var bun = am.LoadBundleFile(path, true);
            var bundleAssetsFile = am.LoadAssetsFileFromBundle(bun, 0);

            //Load AssetBundle asset from Bundle AssetsFile so that we can update its data later
            var assetBundleAsset = bundleAssetsFile.table.GetAssetsOfType((int)AssetClassID.AssetBundle)[0];
            var assetBundleExtAsset = am.GetExtAsset(bundleAssetsFile, 0, assetBundleAsset.index);

            return (bun, bundleAssetsFile, assetBundleExtAsset);
        }

        public static void AddDependency(this AssetsFileInstance assetsFileInst, AssetsFileDependency assetsFileDependency)
        {
            var dependencies = assetsFileInst.file.dependencies.dependencies;

            var assetsFilePath = assetsFileDependency.assetPath;
            var fixedPath = assetsFilePath;
            if (assetsFilePath != Extensions.unityBuiltinExtra && assetsFilePath != Extensions.unityDefaultResources)
            {
                fixedPath = Path.GetFileNameWithoutExtension(assetsFilePath);
                var cabName = $"cab-{HashingMethods.Calculate<MD4>(fixedPath)}";
                fixedPath = $"archive:/{cabName}/{cabName}";
            }

            dependencies.Add(
                new AssetsFileDependency
                {
                    assetPath = fixedPath,
                    originalAssetPath = fixedPath,
                    bufferedPath = string.Empty,
                    guid = assetsFileDependency.guid,
                    type = assetsFileDependency.type
                }
            );
            assetsFileInst.file.dependencies.dependencyCount = dependencies.Count;
            assetsFileInst.file.dependencies.dependencies = dependencies;
        }

        public static void UpdateAssetBundleDependencies(AssetTypeValueField bundleBaseField, List<AssetsFileDependency> dependencies)
        {
            var dependencyArray = bundleBaseField.GetField("m_Dependencies/Array");
            var dependencyFieldChildren = new List<AssetTypeValueField>();

            foreach (var dep in dependencies)
            {
                var depTemplate = ValueBuilder.DefaultValueFieldFromArrayTemplate(dependencyArray);
                depTemplate.GetValue().Set(dep.assetPath);
                dependencyFieldChildren.Add(depTemplate);
            }
            dependencyArray.SetChildrenList(dependencyFieldChildren.ToArray());
        }
        public static void AddDependency(this AssetsFileInstance assetsFileInst, string dependency)
        {
            var dependencies = assetsFileInst.file.dependencies;

            dependencies.dependencies.Add(new AssetsFileDependency
            {
                assetPath = dependency,
                originalAssetPath = dependency,
                bufferedPath = string.Empty
            });

            dependencies.dependencyCount = dependencies.dependencies.Count;
        }
        public static void WriteTextureFile(this TextureFile textureFile, AssetTypeValueField baseField)
        {
            if (!baseField.GetField("m_Name").IsDummy()) baseField.SetValue("m_Name", textureFile.m_Name);
            if (!baseField.GetField("m_ForcedFallbackFormat").IsDummy()) baseField.SetValue("m_ForcedFallbackFormat", textureFile.m_ForcedFallbackFormat);
            if (!baseField.GetField("m_DownscaleFallback").IsDummy()) baseField.SetValue("m_DownscaleFallback", textureFile.m_DownscaleFallback);
            if (!baseField.GetField("m_DownscaleFallback").IsDummy()) baseField.SetValue("m_DownscaleFallback", textureFile.m_DownscaleFallback);
            if (!baseField.GetField("m_Width").IsDummy()) baseField.SetValue("m_Width", textureFile.m_Width);
            if (!baseField.GetField("m_Height").IsDummy()) baseField.SetValue("m_Height", textureFile.m_Height);
            if (!baseField.GetField("m_TextureFormat").IsDummy()) baseField.SetValue("m_TextureFormat", textureFile.m_TextureFormat);
            if (!baseField.GetField("m_MipCount").IsDummy()) baseField.SetValue("m_MipCount", textureFile.m_MipCount);
            if (!baseField.GetField("m_MipMap").IsDummy()) if (!baseField.Get("m_MipMap").IsDummy()) baseField.SetValue("m_MipMap", textureFile.m_MipMap);
            if (!baseField.GetField("m_IsReadable").IsDummy()) baseField.SetValue("m_IsReadable", textureFile.m_IsReadable);
            if (!baseField.GetField("m_ReadAllowed").IsDummy()) baseField.SetValue("m_ReadAllowed", textureFile.m_ReadAllowed);
            if (!baseField.GetField("m_StreamingMipmaps").IsDummy()) baseField.SetValue("m_StreamingMipmaps", textureFile.m_StreamingMipmaps);
            if (!baseField.GetField("m_StreamingMipmapsPriority").IsDummy()) baseField.SetValue("m_StreamingMipmapsPriority", textureFile.m_StreamingMipmapsPriority);
            if (!baseField.GetField("m_ImageCount").IsDummy()) baseField.SetValue("m_ImageCount", textureFile.m_ImageCount);
            if (!baseField.GetField("m_TextureDimension").IsDummy()) baseField.SetValue("m_TextureDimension", textureFile.m_TextureDimension);
            if (!baseField.GetField("m_TextureSettings/m_FilterMode").IsDummy()) baseField.SetValue("m_TextureSettings/m_FilterMode", textureFile.m_TextureSettings.m_FilterMode);
            if (!baseField.GetField("m_TextureSettings/m_Aniso").IsDummy()) baseField.SetValue("m_TextureSettings/m_Aniso", textureFile.m_TextureSettings.m_Aniso);
            if (!baseField.GetField("m_TextureSettings/m_MipBias").IsDummy()) baseField.SetValue("m_TextureSettings/m_MipBias", textureFile.m_TextureSettings.m_MipBias);
            if (!baseField.GetField("m_TextureSettings/m_WrapMode").IsDummy()) baseField.SetValue("m_TextureSettings/m_WrapMode", textureFile.m_TextureSettings.m_WrapMode);
            if (!baseField.GetField("m_TextureSettings/m_WrapU").IsDummy()) baseField.SetValue("m_TextureSettings/m_WrapU", textureFile.m_TextureSettings.m_WrapU);
            if (!baseField.GetField("m_TextureSettings/m_WrapV").IsDummy()) baseField.SetValue("m_TextureSettings/m_WrapV", textureFile.m_TextureSettings.m_WrapV);
            if (!baseField.GetField("m_TextureSettings/m_WrapW").IsDummy()) baseField.SetValue("m_TextureSettings/m_WrapW", textureFile.m_TextureSettings.m_WrapW);

            if (!baseField.GetField("m_LightmapFormat").IsDummy()) baseField.SetValue("m_LightmapFormat", textureFile.m_LightmapFormat);
            if (!baseField.GetField("m_ColorSpace").IsDummy()) baseField.SetValue("m_ColorSpace", textureFile.m_ColorSpace);

            var image_data = baseField.GetField("image data");
            image_data.GetValue().type = EnumValueTypes.ByteArray;
            image_data.templateField.valueType = EnumValueTypes.ByteArray;
            var byteArray = new AssetTypeByteArray()
            {
                size = (uint)textureFile.pictureData.Length,
                data = textureFile.pictureData
            };
            image_data.GetValue().Set(byteArray);
            if (!baseField.GetField("m_CompleteImageSize").IsDummy()) baseField.SetValue("m_CompleteImageSize", textureFile.pictureData.Length);


            if (!baseField.GetField("m_StreamData/offset").IsDummy()) baseField.SetValue("m_StreamData/offset", textureFile.m_StreamData.offset);
            if (!baseField.GetField("m_StreamData/size").IsDummy()) baseField.SetValue("m_StreamData/size", textureFile.m_StreamData.size);
            if (!baseField.GetField("m_StreamData/path").IsDummy()) baseField.SetValue("m_StreamData/path", textureFile.m_StreamData.path);
        }

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
    }

}
