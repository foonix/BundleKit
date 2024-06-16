using AssetsTools.NET;
using AssetsTools.NET.Extra;
using BundleKit.Assets.Replacers;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
            var assetBundleAsset = bundleAssetsFile.file.GetAssetsOfType((int)AssetClassID.AssetBundle)[0];
            var assetBundleExtAsset = am.GetExtAsset(bundleAssetsFile, 0, assetBundleAsset.PathId);

            return (bun, bundleAssetsFile, assetBundleExtAsset);
        }

        // Obsoleted by changes in copy process?
        // the source files may have dependencies, but we only need to
        // ensure the destination doesn't depend on the source.  AssetsReplacer
        /*
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
        */

        // Oboleted by changes in bundle copy process?
        // texFile.WriteTo()
        /*
        public static void WriteTextureFile(this TextureFile textureFile, AssetTypeValueField baseField)
        {
            if (!baseField["m_Name"].IsDummy) baseField["m_Name"].AsString = textureFile.m_Name;
            if (!baseField["m_ForcedFallbackFormat"].IsDummy) baseField["m_ForcedFallbackFormat"].AsInt = textureFile.m_ForcedFallbackFormat;
            if (!baseField["m_DownscaleFallback"].IsDummy) baseField["m_DownscaleFallback"].AsBool = textureFile.m_DownscaleFallback;
            if (!baseField["m_DownscaleFallback"].IsDummy) baseField["m_DownscaleFallback"].AsBool = textureFile.m_DownscaleFallback;
            if (!baseField["m_Width"].IsDummy) baseField["m_Width"].AsInt = textureFile.m_Width;
            if (!baseField["m_Height"].IsDummy) baseField["m_Height"].AsInt = textureFile.m_Height;
            if (!baseField["m_TextureFormat"].IsDummy) baseField["m_TextureFormat"].AsInt = textureFile.m_TextureFormat;
            if (!baseField["m_MipCount"].IsDummy) baseField["m_MipCount"].AsInt = textureFile.m_MipCount;
            if (!baseField["m_MipMap"].IsDummy) if (!baseField["m_MipMap"].IsDummy) baseField["m_MipMap"].AsBool = textureFile.m_MipMap;
            if (!baseField["m_IsReadable"].IsDummy) baseField["m_IsReadable"].AsBool = textureFile.m_IsReadable;
            if (!baseField["m_ReadAllowed"].IsDummy) baseField["m_ReadAllowed"].AsBool = textureFile.m_ReadAllowed;
            if (!baseField["m_StreamingMipmaps"].IsDummy) baseField["m_StreamingMipmaps"].AsBool = textureFile.m_StreamingMipmaps;
            if (!baseField["m_StreamingMipmapsPriority"].IsDummy) baseField["m_StreamingMipmapsPriority"].AsInt = textureFile.m_StreamingMipmapsPriority;
            if (!baseField["m_ImageCount"].IsDummy) baseField["m_ImageCount"].AsInt = textureFile.m_ImageCount;
            if (!baseField["m_TextureDimension"].IsDummy) baseField["m_TextureDimension"].AsInt = textureFile.m_TextureDimension;
            if (!baseField["m_TextureSettings/m_FilterMode"].IsDummy) baseField["m_TextureSettings/m_FilterMode"].AsInt = textureFile.m_TextureSettings.m_FilterMode;
            if (!baseField["m_TextureSettings/m_Aniso"].IsDummy) baseField["m_TextureSettings/m_Aniso"].AsInt = textureFile.m_TextureSettings.m_Aniso;
            if (!baseField["m_TextureSettings/m_MipBias"].IsDummy) baseField["m_TextureSettings/m_MipBias"].AsFloat = textureFile.m_TextureSettings.m_MipBias;
            if (!baseField["m_TextureSettings/m_WrapMode"].IsDummy) baseField["m_TextureSettings/m_WrapMode"].AsInt = textureFile.m_TextureSettings.m_WrapMode;
            if (!baseField["m_TextureSettings/m_WrapU"].IsDummy) baseField["m_TextureSettings/m_WrapU"].AsInt = textureFile.m_TextureSettings.m_WrapU;
            if (!baseField["m_TextureSettings/m_WrapV"].IsDummy) baseField["m_TextureSettings/m_WrapV"].AsInt = textureFile.m_TextureSettings.m_WrapV;
            if (!baseField["m_TextureSettings/m_WrapW"].IsDummy) baseField["m_TextureSettings/m_WrapW"].AsInt = textureFile.m_TextureSettings.m_WrapW;

            if (!baseField["m_LightmapFormat"].IsDummy) baseField["m_LightmapFormat"].AsInt = textureFile.m_LightmapFormat;
            if (!baseField["m_ColorSpace"].IsDummy) baseField["m_ColorSpace"].AsInt = textureFile.m_ColorSpace;

            var image_data = baseField["image data"];
            //image_data.GetValue().type = AssetValueType.ByteArray;
            //image_data.TemplateField.valueType = AssetValueType.ByteArray;
            //var byteArray = new AssetTypeByteArray()
            //{
            //    size = (uint)textureFile.pictureData.Length,
            //    data = textureFile.pictureData
            //};
            //image_data.Value = byteArray;
            image_data.Value.ValueType = AssetValueType.ByteArray;
            image_data.AsByteArray = textureFile.pictureData;

            if (!baseField["m_CompleteImageSize"].IsDummy) baseField["m_CompleteImageSize"].AsInt = textureFile.pictureData.Length;
            if (!baseField["m_StreamData/offset"].IsDummy) baseField["m_StreamData/offset"].AsULong = textureFile.m_StreamData.offset;
            if (!baseField["m_StreamData/size"].IsDummy) baseField["m_StreamData/size"].AsUInt = textureFile.m_StreamData.size;
            if (!baseField["m_StreamData/path"].IsDummy) baseField["m_StreamData/path"].AsString = textureFile.m_StreamData.path;
        }
        */

        // No references.  Still needed?
        /*
        public static IEnumerable<AssetTypeValueField> FindFieldType(this AssetTypeValueField valueField, Predicate<string> typeMatch)
        {
            var fieldStack = new Stack<AssetTypeValueField>();
            AssetTypeValueField field = null;
            fieldStack.Push(valueField);
            while (fieldStack.Any())
            {
                field = fieldStack.Pop();
                if (field.ChildrenCount > 0)
                {
                    string typeName = field.TemplateField.type;
                    if (typeMatch(typeName))
                    {
                        yield return field;
                    }
                    foreach (var child in field.Children)
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
                if (field.ChildrenCount > 0)
                {
                    var targetField = field.Get(fieldPath);
                    if (targetField.ChildrenCount > -1)
                    {
                        yield return targetField;
                    }
                    foreach (var child in field.Children)
                        fieldStack.Push(child);
                }
            }
        }
        */

        public static void RemapPPtrs(this AssetTypeValueField field, IDictionary<(int fileId, long pathId), (int fileId, long pathId)> map)
        {
            var fieldStack = new Stack<AssetTypeValueField>();
            fieldStack.Push(field);
            while (fieldStack.Any())
            {
                var current = fieldStack.Pop();
                foreach (AssetTypeValueField child in current.Children)
                {
                    //not a value (ie not an int)
                    if (!child.TemplateField.HasValue)
                    {
                        //not array of values either
                        if (child.TemplateField.IsArray && child.TemplateField.Children[1].ValueType != AssetValueType.None)
                            continue;

                        string typeName = child.TemplateField.Type;
                        //is a pptr
                        if (typeName.StartsWith("PPtr<") && typeName.EndsWith(">"))
                        {
                            var fileIdField = child["m_FileID"];
                            var pathIdField = child["m_PathID"];
                            var pathId = pathIdField.AsLong;
                            var fileId = fileIdField.AsInt;
                            if (!map.ContainsKey((fileId, pathId))) continue;

                            var newPPtr = map[(fileId, pathId)];
                            fileIdField.AsInt = newPPtr.fileId;
                            pathIdField.AsLong = newPPtr.pathId;
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
            pair["first"].AsString = name;
            pair["second"]["preloadIndex"].AsInt = preloadIndex;
            pair["second"]["preloadSize"].AsInt = preloadSize;
            pair["second"]["asset"]["m_FileID"].AsInt = fileId;
            pair["second"]["asset"]["m_PathID"].AsLong = pathId;
            return pair;
        }

        /// <summary>
        /// Create an AssetBundleFile from scratch and initialize for the current unity version.
        ///
        /// Any assets the bundle shoud contain would be in an AssetsFile structure
        /// wrapped in a AssetBundleDirectoryInfo (with a ContentReplacer) and added to
        /// BlockAndDirInfo.DirectoryInfos.
        /// </summary>
        /// <returns>A minimally initialized AssetBundleFile</returns>
        public static AssetBundleFile CreateEmptyAssetBundle()
        {
            // Most of this isn't used outside of bundle reading/writing a bundle
            // So we need to set our write intent here.
            AssetBundleFile file = new()
            {
                Header = new AssetBundleHeader()
                {
                    EngineVersion = Application.unityVersion,
                    Signature = "UnityFS",
                    GenerationVersion = "5.x.x",
                    Version = 8, // 6, 7, or 8
                    FileStreamHeader = new()
                    {
                        // Sizes are calculated at write time.
                        Flags = AssetBundleFSHeaderFlags.HasDirectoryInfo
                    },
                },
                BlockAndDirInfo = new AssetBundleBlockAndDirInfo()
                {
                    // needed to put assets into a directory
                    DirectoryInfos = new(),
                    BlockInfos = new AssetBundleBlockInfo[]
                    {
                        new()
                        {
                            Flags = 0x40, // don't stream
                        },
                    },
                },
            };

            return file;
        }

        /// <summary>
        /// Create from scratch an AssetsFile suitable for use in an AssetBundle.
        /// </summary>
        /// <param name="cabName">Name of the CAB object. (Type AssetClassID.AssetBundle = 142)</param>
        /// <param name="cldb">Type database to initialize cabBaseField</param>
        /// <param name="assetsFile">A (mostly empty) AssetsFile.</param>
        /// <param name="cabBaseField"></param>
        public static void CreateBundleAssetsFile(string cabName, ClassDatabaseFile cldb, out AssetsFile assetsFile, out AssetTypeValueField cabBaseField)
        {
            assetsFile = new AssetsFile()
            {
                Header = new()
                {
                    Version = 22, // 2020.x and up
                },
                Metadata = new()
                {
                    TypeTreeEnabled = true,
                    UnityVersion = Application.unityVersion,
                    RefTypes = new(),
                    TypeTreeTypes = new(),
                    ScriptTypes = new(),
                    AssetInfos = new List<AssetFileInfo>(),
                    Externals = new(),
                    UserInformation = "BundleKit generated bundle",
                },
            };

            // setup empty CAB as first asset. (pathId 1)
            AssetTypeTemplateField templateField = new()
            {
                Children = new(),
            };

            var cldbType = cldb.FindAssetClassByID((int)AssetClassID.AssetBundle);
            templateField.FromClassDatabase(cldb, cldbType);
            cabBaseField = ValueBuilder.DefaultValueFieldFromTemplate(templateField);

            // The Unity editor will cowardly refuse to load the bundle if this is not set.
            cabBaseField["m_RuntimeCompatibility"].AsUInt = 1;
            cabBaseField["m_Name"].AsString = cabName;
            cabBaseField["m_AssetBundleName"].AsString = cabName;

            var cabDataInfo = AssetFileInfo.Create(assetsFile, 1, (int)AssetClassID.AssetBundle, cldb);
            cabDataInfo.Replacer = new DeferredBaseFieldSerializer(cabBaseField);
            assetsFile.AssetInfos.Add(cabDataInfo);
        }
    }
}
