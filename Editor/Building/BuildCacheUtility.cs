using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEditor.Build.Player;
using UnityEngine;
using static BundleKit.Utility.Extensions;

namespace BundleKit.Building
{
    /// <summary>
    /// Wrapper for reflection calls to internal BuildCacheUtility class
    /// </summary>
    public static class BuildCacheUtility
    {

        private static readonly Type buildCacheUtilityType = typeof(ContentPipeline).Assembly.GetType("BuildCacheUtility");
        private static readonly MethodInfo getMainTypeForObjectMethod = buildCacheUtilityType.GetMethod(nameof(GetMainTypeForObject), BindingFlags.Public | BindingFlags.Static);
        private static readonly MethodInfo getMainTypeForObjectsMethod = buildCacheUtilityType.GetMethod(nameof(GetMainTypeForObjects), BindingFlags.Public | BindingFlags.Static);

        static Dictionary<KeyValuePair<GUID, int>, CacheEntry> m_GuidToHash = new Dictionary<KeyValuePair<GUID, int>, CacheEntry>();
        static Dictionary<KeyValuePair<string, int>, CacheEntry> m_PathToHash = new Dictionary<KeyValuePair<string, int>, CacheEntry>();
        static Dictionary<KeyValuePair<Type, int>, CacheEntry> m_TypeToHash = new Dictionary<KeyValuePair<Type, int>, CacheEntry>();
        static Dictionary<ObjectIdentifier, Type[]> m_ObjectToType = new Dictionary<ObjectIdentifier, Type[]>();
        static TypeDB m_TypeDB;

#if !ENABLE_TYPE_HASHING
        static Hash128 m_UnityVersion = HashingMethods.Calculate(Application.unityVersion).ToHash128();
#endif

        static Type[] GetCachedTypesForObject(ObjectIdentifier objectId)
        {
            if (!m_ObjectToType.TryGetValue(objectId, out Type[] types))
            {
#if ENABLE_TYPE_HASHING
            types = ContentBuildInterface.GetTypesForObject(objectId);
#else
                types = ContentBuildInterface.GetTypeForObjects(new[] { objectId });
#endif
                m_ObjectToType[objectId] = types;
            }
            return types;
        }

        public static Type GetMainTypeForObject(ObjectIdentifier objectId)
        {
            Type[] types = GetCachedTypesForObject(objectId);
            return types[0];
        }

        public static Type[] GetMainTypeForObjects(IEnumerable<ObjectIdentifier> objectIds)
        {
            List<Type> results = new List<Type>();
            foreach (var objectId in objectIds)
            {
                Type[] types = GetCachedTypesForObject(objectId);
                results.Add(types[0]);
            }
            return results.ToArray();
        }

        public static Type[] GetSortedUniqueTypesForObject(ObjectIdentifier objectId)
        {
            Type[] types = GetCachedTypesForObject(objectId);
            Array.Sort(types, (x, y) => x.AssemblyQualifiedName.CompareTo(y.AssemblyQualifiedName));
            return types;
        }

        public static Type[] GetSortedUniqueTypesForObjects(IEnumerable<ObjectIdentifier> objectIds)
        {
            Type[] types;
            HashSet<Type> results = new HashSet<Type>();
            foreach (var objectId in objectIds)
            {
                types = GetCachedTypesForObject(objectId);
                results.UnionWith(types);
            }
            types = results.ToArray();
            Array.Sort(types, (x, y) => x.AssemblyQualifiedName.CompareTo(y.AssemblyQualifiedName));
            return types;
        }

        public static void SetTypeForObjects(IEnumerable<ObjectTypes> pairs)
        {
            foreach (var pair in pairs)
                m_ObjectToType[pair.ObjectID] = pair.Types;
        }

        internal static void ClearCacheHashes()
        {
            m_GuidToHash.Clear();
            m_PathToHash.Clear();
            m_TypeToHash.Clear();
            m_ObjectToType.Clear();
            m_TypeDB = null;
        }

        public static void SetTypeDB(TypeDB typeDB)
        {
            if (m_TypeToHash.Count > 0)
                throw new InvalidOperationException("Changing Player TypeDB mid build is not supported at this time.");
            m_TypeDB = typeDB;
        }

    }
}
