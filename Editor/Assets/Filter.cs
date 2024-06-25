using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BundleKit.Assets
{
    [Serializable]
    public struct Filter
    {
        [Tooltip("An object must have a name matching one of these expressions.  Leave this list empty to match any name.")]
        public string[] nameRegex;
        [Tooltip("Unity built-in object class required for this filter to match.  Use 'Object' to match any kind of object.")]
        public AssetClassID assetClass;

        private Regex[] regexCache;

        public bool Match(AssetFileInfo assetFileInfo, string name)
        {
            if (!((AssetClassID)assetFileInfo.TypeId == assetClass))
            {
                return false;
            }

            // match all objects with the given class if not filtering by name.
            if (nameRegex.Length == 0)
            {
                return true;
            }

            regexCache ??= nameRegex.Select(p => new Regex(p, RegexOptions.IgnoreCase)).ToArray();

            foreach (var regex in regexCache)
            {
                if (regex.IsMatch(name))
                {
                    return true;
                }
            }

            return false;
        }
    }
}