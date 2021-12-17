using BundleKit.Building.Contexts;
using BundleKit.Bundles;
using BundleKit.Utility;
using System.Collections.Generic;
using System.Linq;
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

        /// <inheritdoc />
        public int Version { get { return 3; } }

        public ReturnCode Run()
        {
            Dictionary<string, WriteCommand> fileToCommand;
            var forwardObjectDependencies = new Dictionary<string, HashSet<ObjectIdentifier>>();
            var forwardFileDependencies = new Dictionary<string, HashSet<string>>();
            var reverseAssetDependencies = new Dictionary<string, HashSet<GUID>>();

            // BuildReferenceMap details what objects exist in other bundles that objects in a source bundle depend upon (forward dependencies)
            // BuildUsageTagSet details the conditional data needed to be written by objects in a source bundle that is in used by objects in other bundles (reverse dependencies)
            using (m_Log.ScopedStep(LogLevel.Info, "Temporary Map Creations"))
            {
                fileToCommand = m_WriteData.WriteOperations.ToDictionary((x) => x.Command.internalName, (x) => x.Command);
                foreach (var kvp in fileToCommand)
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

                foreach (var assetToFile in m_WriteData.AssetToFiles)
                {
                    var asset = assetToFile.Key;
                    var files = assetToFile.Value;

                    // The includes for an asset live in the first file, references could live in any file
                    forwardObjectDependencies.GetOrAdd(files[0], out HashSet<ObjectIdentifier> objectDependencies);
                    forwardFileDependencies.GetOrAdd(files[0], out HashSet<string> fileDependencies);

                    // Grab the list of object references for the asset or scene and add them to the forward dependencies hash set for this file (write command)
                    if (m_DependencyData.AssetInfo.TryGetValue(asset, out var assetInfo))
                        objectDependencies.UnionWith(assetInfo.referencedObjects);

                    if (m_DependencyData.SceneInfo.TryGetValue(asset, out var sceneInfo))
                    {
                        if (sceneInfo.referencedObjects.Any(robj => robj.filePath.Equals("archive:/resources.assets")))
                        {
                            if (!files.Contains("resources.assets"))
                                files.Add("resources.assets");

                            if (!fileDependencies.Contains("resources.assets"))
                                fileDependencies.Add("resources.assets");
                        }
                        objectDependencies.UnionWith(sceneInfo.referencedObjects);

                    }

                    // Grab the list of file references for the asset or scene and add them to the forward dependencies hash set for this file (write command)
                    // While doing so, also add the asset to the reverse dependencies hash set for all the other files it depends upon.
                    // We already ensure BuildReferenceMap & BuildUsageTagSet contain the objects in this write command in GenerateBundleCommands. So skip over the first file (self)
                    for (int i = 1; i < files.Count; i++)
                    {
                        fileDependencies.Add(files[i]);
                        reverseAssetDependencies.GetOrAdd(files[i], out HashSet<GUID> reverseDependencies);
                        reverseDependencies.Add(asset);
                    }
                }
            }

            // Using the previously generated forward dependency maps, update the BuildReferenceMap per WriteCommand to contain just the references that we care about
            using (m_Log.ScopedStep(LogLevel.Info, "Populate BuildReferenceMaps"))
            {
                foreach (IWriteOperation writeOperation in m_WriteData.WriteOperations)
                {
                    var internalName = writeOperation.Command.internalName;
                    var buildReferenceMap = m_WriteData.FileToReferenceMap[internalName];

                    if (!forwardObjectDependencies.TryGetValue(internalName, out var objectDependencies)) continue; // this bundle has no external dependencies
                    if (!forwardFileDependencies.TryGetValue(internalName, out var fileDependencies)) continue; // this bundle has no external dependencies

                    foreach (string file in fileDependencies)
                    {
                        WriteCommand writeCommand = fileToCommand[file];
                        foreach (SerializationInfo serializeObject in writeCommand.serializeObjects)
                        {
                            // Only add objects we are referencing. This ensures that new/removed objects to files we depend upon will not cause a rebuild
                            // of this file, unless are referencing the new/removed objects.
                            if (!objectDependencies.Contains(serializeObject.serializationObject)) continue;

                            buildReferenceMap.AddMapping(file, serializeObject.serializationIndex, serializeObject.serializationObject);
                        }
                    }
                }
            }

            // Using the previously generate reverse dependency map, create the BuildUsageTagSet per WriteCommand to contain just the data that we care about
            using (m_Log.ScopedStep(LogLevel.Info, "Populate BuildUsageTagSet"))
            {
                foreach (IWriteOperation writeOperation2 in m_WriteData.WriteOperations)
                {
                    var internalName = writeOperation2.Command.internalName;
                    BuildUsageTagSet buildUsageTagSet = m_WriteData.FileToUsageSet[internalName];
                    if (reverseAssetDependencies.TryGetValue(internalName, out var assetDependencies))
                    {
                        foreach (GUID item2 in assetDependencies)
                        {
                            if (m_DependencyData.AssetUsage.TryGetValue(item2, out var assetUsage))
                            {
                                buildUsageTagSet.UnionWith(assetUsage);
                            }

                            if (m_DependencyData.SceneUsage.TryGetValue(item2, out var sceneUsage))
                            {
                                buildUsageTagSet.UnionWith(sceneUsage);
                            }
                        }
                    }

                    if (ReflectionExtensions.SupportsFilterToSubset)
                    {
                        buildUsageTagSet.FilterToSubset(m_WriteData.FileToObjects[internalName].ToArray());
                    }
                }
            }

            return ReturnCode.Success;
        }
    }
}