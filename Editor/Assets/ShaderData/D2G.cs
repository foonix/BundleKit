using System;
using System.Runtime.InteropServices;

namespace BundleKit.Assets.ShaderData
{
    public class D2G
    {
        [DllImport("dxbc2glsl.dll", CharSet = CharSet.Ansi)]
        public extern static IntPtr decompile_to_hlsl(byte[] input, UIntPtr inputLength);
        
    }
}