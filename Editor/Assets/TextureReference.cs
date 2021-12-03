using System;
using UnityEngine;

namespace BundleKit.Assets
{
    [Serializable]
    struct TextureReference
    {
        public long localId;
        public string guid;
        public Vector2 offset;
        public Vector2 scale;
    }
}
