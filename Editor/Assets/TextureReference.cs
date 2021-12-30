using System;
using UnityEngine;

namespace BundleKit.Assets
{
    [Serializable]
    public struct TextureReference
    {
        public long localId;
        public string guid;
        public Vector2 offset;
        public Vector2 scale;
    }
}
