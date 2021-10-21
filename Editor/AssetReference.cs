using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace InBundleResourceReference
{
    [Serializable]
    public class AssetReference
    {
        public long originalPathID;
        public UnityEngine.Object asset;
    }
}