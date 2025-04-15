using BundleKit.Utility;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace BundleKit.Assets.ShaderData
{
    public class ShaderVariant
    {
        readonly HashSet<string> keywords = new();
        readonly byte[] shaderObj;

        public ShaderVariant(HashSet<string> keywords, byte[] shaderObj)
        {
            this.keywords = keywords;
            this.shaderObj = shaderObj;
        }

        public static ShaderVariant FromBlobSpan(ReadOnlySpan<byte> blobSpan)
        {
            // I can't find a direct offset past the keyword strings.
            // We have to loop through them to get to the DXBG start.
            // so I'm assuming the header is a constant length, followed by variable keyword array, followed by constant length.

            HashSet<string> keywords = new();

            // The 6th dword is the keyword count.
            int cursor = 6 * sizeof(int);
            var stringCount = blobSpan.Read<int>(ref cursor);
            for (int i = 0; i < stringCount; i++)
            {
                keywords.Add(blobSpan.ReadPaddedString(ref cursor).ToString());
            }

            // skip byes to DXBC.  Start does not seem to be a padded offset.
            cursor += 0x2A;
            if (!(
                blobSpan[cursor] == 'D'
                && blobSpan[cursor + 1] == 'X'
                && blobSpan[cursor + 2] == 'B'
                && blobSpan[cursor + 3] == 'C'
                ))
            {
                // something went wrong
                Debug.LogError("DirectX shader object header not found.");
            }
            var thing = blobSpan.Slice(cursor);
            return new ShaderVariant(keywords, thing.ToArray());
        }
    }
}