using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEditor.Build.Player;

namespace BundleKit.Building
{
    /// <summary>
    /// Wrapper for reflection calls to internal BuildCacheUtility class
    /// </summary>
    public static class BuildCacheUtilityWrapper
    {

        private static readonly Type buildCacheUtilityType = typeof(ContentPipeline).Assembly.GetType("BuildCacheUtility");
        private static readonly MethodInfo getMainTypeForObjectMethod = buildCacheUtilityType.GetMethod(nameof(GetMainTypeForObject), BindingFlags.Public | BindingFlags.Static);
        private static readonly MethodInfo getMainTypeForObjectsMethod = buildCacheUtilityType.GetMethod(nameof(GetMainTypeForObjects), BindingFlags.Public | BindingFlags.Static);

        public static Type GetMainTypeForObject(ObjectIdentifier objectId)
        {
            return getMainTypeForObjectMethod.Invoke(null, new object[] { objectId }) as Type;
        }

        public static Type[] GetMainTypeForObjects(IEnumerable<ObjectIdentifier> objectIds)
        {
            return  getMainTypeForObjectsMethod.Invoke(null, new object[] { objectIds }) as Type[];
        }
    }
}
