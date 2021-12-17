using BundleKit.Building.Contexts;
using BundleKit.Bundles;
using BundleKit.Utility;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Injector;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Utilities;

namespace BundleKit.Building
{
    public class GenerateBundleMaps : IBuildTask
    {
#pragma warning disable IDE0044
#pragma warning disable 649
        [InjectContext(ContextUsage.In, false)]
        private IDependencyData m_DependencyData;

        [InjectContext(ContextUsage.InOut, false)]
        private IBundleWriteData m_WriteData;

        [InjectContext(ContextUsage.In, true)]
        private IBuildLogger m_Log;

        [InjectContext(ContextUsage.InOut, true)]
        private IAssetMapsContext m_AssetMapData;

        [InjectContext(ContextUsage.In, true)]
        private IAssetsReference m_AssetsReference;
#pragma warning restore 649 
#pragma warning disable IDE0044

        public int Version => 1;

        public ReturnCode Run()
        {
            Dictionary<string, WriteCommand> dictionary;
            Dictionary<string, HashSet<ObjectIdentifier>> dictionary2;
            Dictionary<string, HashSet<string>> dictionary3;
            Dictionary<string, HashSet<GUID>> dictionary4;
            using (m_Log.ScopedStep(LogLevel.Info, "Temporary Map Creations"))
            {
                dictionary = m_WriteData.WriteOperations.ToDictionary((IWriteOperation x) => x.Command.internalName, (IWriteOperation x) => x.Command);
                foreach (var kvp in dictionary)
                {
                    var name = kvp.Key;
                    var command = kvp.Value;
                    var fileName = command.fileName;
                    var internalName = command.internalName;
                    for (int i = 0; i < command.serializeObjects.Count; i++)
                    {
                        var serializedObj = command.serializeObjects[i];
                        var newId = serializedObj.serializationIndex;
                        var oldId = serializedObj.serializationObject.localIdentifierInFile;
                        var filePath = serializedObj.serializationObject.filePath;
                        if (filePath == "archive:/resources.assets" && m_AssetMapData != null && m_AssetsReference != null)
                        {
                            var map = m_AssetsReference.MappingData.AssetMaps.FirstOrDefault(m => m.BundlePointer.pathId == oldId);
                            if (map != default)
                                m_AssetMapData.AssetMaps.Add(new Assets.AssetMap
                                {
                                    name = fileName,
                                    BundlePointer = (0, newId),
                                    ResourcePointer = map.ResourcePointer
                                });
                        }
                    }
                }
                dictionary2 = new Dictionary<string, HashSet<ObjectIdentifier>>();
                dictionary3 = new Dictionary<string, HashSet<string>>();
                dictionary4 = new Dictionary<string, HashSet<GUID>>();
                foreach (KeyValuePair<GUID, List<string>> assetToFile in m_WriteData.AssetToFiles)
                {
                    GUID key = assetToFile.Key;
                    List<string> value = assetToFile.Value;
                    dictionary2.GetOrAdd(value[0], out HashSet<ObjectIdentifier> value2);
                    dictionary3.GetOrAdd(value[0], out HashSet<string> value3);
                    if (m_DependencyData.AssetInfo.TryGetValue(key, out AssetLoadInfo value4))
                    {
                        value2.UnionWith(value4.referencedObjects);
                    }

                    if (m_DependencyData.SceneInfo.TryGetValue(key, out SceneDependencyInfo value5))
                    {
                        value2.UnionWith(value5.referencedObjects);
                    }

                    for (int i = 1; i < value.Count; i++)
                    {
                        value3.Add(value[i]);
                        dictionary4.GetOrAdd(value[i], out HashSet<GUID> value6);
                        value6.Add(key);
                    }
                }
            }

            using (m_Log.ScopedStep(LogLevel.Info, "Populate BuildReferenceMaps"))
            {
                foreach (IWriteOperation writeOperation in m_WriteData.WriteOperations)
                {
                    string internalName = writeOperation.Command.internalName;
                    BuildReferenceMap buildReferenceMap = m_WriteData.FileToReferenceMap[internalName];
                    if (!dictionary2.TryGetValue(internalName, out HashSet<ObjectIdentifier> value7) || !dictionary3.TryGetValue(internalName, out HashSet<string> value8))
                    {
                        continue;
                    }

                    foreach (string item in value8)
                    {
                        WriteCommand writeCommand = dictionary[item];
                        foreach (SerializationInfo serializeObject in writeCommand.serializeObjects)
                        {
                            if (value7.Contains(serializeObject.serializationObject))
                            {
                                buildReferenceMap.AddMapping(item, serializeObject.serializationIndex, serializeObject.serializationObject);
                            }
                        }
                    }
                }
            }

            using (m_Log.ScopedStep(LogLevel.Info, "Populate BuildUsageTagSet"))
            {
                foreach (IWriteOperation writeOperation2 in m_WriteData.WriteOperations)
                {
                    string internalName2 = writeOperation2.Command.internalName;
                    BuildUsageTagSet buildUsageTagSet = m_WriteData.FileToUsageSet[internalName2];
                    if (dictionary4.TryGetValue(internalName2, out HashSet<GUID> value9))
                    {
                        foreach (GUID item2 in value9)
                        {
                            if (m_DependencyData.AssetUsage.TryGetValue(item2, out BuildUsageTagSet value10))
                            {
                                buildUsageTagSet.UnionWith(value10);
                            }

                            if (m_DependencyData.SceneUsage.TryGetValue(item2, out BuildUsageTagSet value11))
                            {
                                buildUsageTagSet.UnionWith(value11);
                            }
                        }
                    }

                    if (ReflectionExtensions.SupportsFilterToSubset)
                    {
                        buildUsageTagSet.FilterToSubset(m_WriteData.FileToObjects[internalName2].ToArray());
                    }
                }
            }

            return ReturnCode.Success;
        }
    }
}