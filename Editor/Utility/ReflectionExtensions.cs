using System;
using System.Linq;
using System.Reflection;
using UnityEditor.Build.Content;

namespace BundleKit.Utility
{
    internal static class ReflectionExtensions
    {
        private static FieldInfo WriteResult_SerializedObjects;

        private static FieldInfo WriteResult_ResourceFiles;

        private static FieldInfo SceneDependencyInfo_Scene;

        private static FieldInfo SceneDependencyInfo_ProcessedScene;

        private static FieldInfo SceneDependencyInfo_ReferencedObjects;

        private static bool BuildUsageTagSet_SupportsFilterToSubset;

        private static bool ContentBuildInterface_SupportsMultiThreadedArchiving;

        public static bool SupportsFilterToSubset => BuildUsageTagSet_SupportsFilterToSubset;

        public static bool SupportsMultiThreadedArchiving => ContentBuildInterface_SupportsMultiThreadedArchiving;

        static ReflectionExtensions()
        {
            WriteResult_SerializedObjects = typeof(WriteResult).GetField("m_SerializedObjects", BindingFlags.Instance | BindingFlags.NonPublic);
            WriteResult_ResourceFiles = typeof(WriteResult).GetField("m_ResourceFiles", BindingFlags.Instance | BindingFlags.NonPublic);
            SceneDependencyInfo_Scene = typeof(SceneDependencyInfo).GetField("m_Scene", BindingFlags.Instance | BindingFlags.NonPublic);
            SceneDependencyInfo_ProcessedScene = typeof(SceneDependencyInfo).GetField("m_ProcessedScene", BindingFlags.Instance | BindingFlags.NonPublic);
            SceneDependencyInfo_ReferencedObjects = typeof(SceneDependencyInfo).GetField("m_ReferencedObjects", BindingFlags.Instance | BindingFlags.NonPublic);
            BuildUsageTagSet_SupportsFilterToSubset = (typeof(BuildUsageTagSet).GetMethod("FilterToSubset") != null);
            foreach (MethodInfo item in from x in typeof(ContentBuildInterface).GetMethods()
                                        where x.Name == "ArchiveAndCompress"
                                        select x)
            {
                foreach (CustomAttributeData customAttribute in item.CustomAttributes)
                {
                    ContentBuildInterface_SupportsMultiThreadedArchiving = (customAttribute.AttributeType.Name == "ThreadSafeAttribute");
                    if (ContentBuildInterface_SupportsMultiThreadedArchiving)
                    {
                        break;
                    }
                }
            }
        }

        public static void SetSerializedObjects(this ref WriteResult result, ObjectSerializedInfo[] osis)
        {
            object obj = result;
            WriteResult_SerializedObjects.SetValue(obj, osis);
            result = (WriteResult)obj;
        }

        public static void SetResourceFiles(this ref WriteResult result, ResourceFile[] files)
        {
            object obj = result;
            WriteResult_ResourceFiles.SetValue(obj, files);
            result = (WriteResult)obj;
        }

        public static void SetScene(this ref SceneDependencyInfo dependencyInfo, string scene)
        {
            object obj = dependencyInfo;
            SceneDependencyInfo_Scene.SetValue(obj, scene);
            dependencyInfo = (SceneDependencyInfo)obj;
        }

        public static void SetReferencedObjects(this ref SceneDependencyInfo dependencyInfo, ObjectIdentifier[] references)
        {
            object obj = dependencyInfo;
            SceneDependencyInfo_ReferencedObjects.SetValue(obj, references);
            dependencyInfo = (SceneDependencyInfo)obj;
        }

        public static void FilterToSubset(this BuildUsageTagSet usageSet, ObjectIdentifier[] objectIds)
        {
            throw new Exception("FilterToSubset is not supported in this Unity version");
        }
    }
}