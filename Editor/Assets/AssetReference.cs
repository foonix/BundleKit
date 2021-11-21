using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BundleKit.Assets
{
    [Serializable]
    public class AssetReference
    {
        public long originalPathID;
        public UnityEngine.Object asset;
    }
}