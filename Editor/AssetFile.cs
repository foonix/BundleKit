using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace InBundleResourceReference
{
    [Serializable]
    public class AssetFile
    {
        public string fileName;
        public bool isAssetBundle;
        public string dependencyString;
        public string relativeFolder;
    }
}