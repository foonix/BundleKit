using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build.Pipeline;

namespace BundleKit.Building
{
    /// <summary>
    /// Wrapper for reflection calls to internal ValidationMethods class
    /// </summary>
    public static class ValidationMethodsWrapper
    {
        public enum Status
        {
            Invalid,
            Asset,
            Scene
        }

        private static readonly Type validMethodsType = typeof(ContentPipeline).Assembly.GetType("UnityEditor.Build.Pipeline.Utilities.ValidationMethods");
        private static readonly MethodInfo validAssetMethod = validMethodsType.GetMethod(nameof(ValidAsset), BindingFlags.Public | BindingFlags.Static);
        private static readonly MethodInfo validSceneBundleMethod = validMethodsType.GetMethod(nameof(ValidSceneBundle), BindingFlags.Public | BindingFlags.Static);
        private static readonly MethodInfo validAssetBundleMethod = validMethodsType.GetMethod(nameof(ValidAssetBundle), BindingFlags.Public | BindingFlags.Static);
        private static readonly MethodInfo hasDirtyScenesMethod = validMethodsType.GetMethod(nameof(HasDirtyScenes), BindingFlags.Public | BindingFlags.Static);

        public static Status ValidAsset(GUID asset)
        {
            return (Status)validAssetMethod.Invoke(null, new object[] { asset });
        }

        public static bool ValidSceneBundle(List<GUID> assets)
        {
            return (bool)validSceneBundleMethod.Invoke(null, new object[] { assets });
        }

        public static bool ValidAssetBundle(List<GUID> assets)
        {
            return (bool)validAssetBundleMethod.Invoke(null, new object[] { assets });
        }

        public static bool HasDirtyScenes()
        {
            return (bool)hasDirtyScenesMethod.Invoke(null, Array.Empty<object>());
        }
    }
}
