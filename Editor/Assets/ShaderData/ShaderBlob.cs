using AssetsTools.NET;
using LZ4ps;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;

namespace BundleKit.Assets.ShaderData
{
    public class ShaderBlob
    {
        readonly int blobIndexCount;
        readonly byte[][] chunks;

        readonly int[] compressedLengths;
        readonly int[] decompressedLengths;

        [StructLayout(LayoutKind.Sequential)]
        private struct BlobAdressData
        {
            /// <summary>
            /// Absolute offset from the start of a decompressed chunk.
            /// </summary>
            public int offset;
            /// <summary>
            /// Size the thing being pointed to.
            /// </summary>
            public int size;
            /// <summary>
            /// Which LZ4 chunk the offset is relative to.
            /// </summary>
            public int chunk;

            public override readonly string ToString() => $"offset:{offset:x} size:{size:x} chunk:{chunk:x}";
        }

        public ShaderBlob(AssetTypeValueField baseField)
        {
            // one of these arrays is possibly related to m_Platforms?
            compressedLengths = baseField["compressedLengths"].Children[0].Children[0].Children[0].Children
                .Select(l => l.AsInt).ToArray();
            decompressedLengths = baseField["decompressedLengths"].Children[0].Children[0].Children[0].Children
                .Select(l => l.AsInt).ToArray();
            var compressedBlob = baseField["compressedBlob"][0].AsByteArray;

            //File.WriteAllBytes(Path.GetFileName(name) + ".blob", compressedBlob);
            chunks = new byte[compressedLengths.Length][];

            int srcOffset = 0;
            for (int i = 0; i < compressedLengths.Length; i++)
            {
                int chunkCompressedLen = compressedLengths[i];
                int chunkDecompresedLen = decompressedLengths[i];
                byte[] decompressedChunk = LZ4Codec.Decode64(compressedBlob, srcOffset, chunkCompressedLen, chunkDecompresedLen);
                chunks[i] = decompressedChunk;
                srcOffset += chunkCompressedLen;
            }

            blobIndexCount = BitConverter.ToInt32(chunks[0], 0);
        }

        private ReadOnlySpan<BlobAdressData> OffsetTable
        {
            get
            {
                var offsetTableSpan = new ReadOnlySpan<byte>(chunks[0], sizeof(int), Marshal.SizeOf<BlobAdressData>() * blobIndexCount);
                return MemoryMarshal.Cast<byte, BlobAdressData>(offsetTableSpan);
            }
        }

        public ReadOnlySpan<byte> GetSpanForBlobIndex(int i)
        {
            var location = OffsetTable[i];

            var span = new ReadOnlySpan<byte>(chunks[location.chunk], location.offset, location.size);

            // Sanity check magic number at blob index target.
            //var magicSlice = span[..4];
            var magic = MemoryMarshal.Read<int>(span);
            if (magic != 0x0C0A75BA)
            {
                Debug.Log($"Wrong magic at blob index {i}, location:{location.offset:x}");
            }

            return span;
        }

        public ShaderVariant ExtractVariantAtIndex(int i)
        {
            return ShaderVariant.FromBlobSpan(GetSpanForBlobIndex(i));
        }
    }
}