using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;
using static BundleKit.Assets.ShaderData.HLSLcc.GLSLCrossDependencyData;

namespace BundleKit.Assets.ShaderData
{
    public class HLSLcc
    {
        public enum GLLang
        {
            LANG_DEFAULT,// Depends on the HLSL shader model.
            LANG_ES_100, LANG_ES_FIRST = LANG_ES_100,
            LANG_ES_300,
            LANG_ES_310, LANG_ES_LAST = LANG_ES_310,
            LANG_120, LANG_GL_FIRST = LANG_120,
            LANG_130,
            LANG_140,
            LANG_150,
            LANG_330,
            LANG_400,
            LANG_410,
            LANG_420,
            LANG_430,
            LANG_440, LANG_GL_LAST = LANG_440,
            LANG_METAL,
        }

        public struct GlExtensions
        {
            /// <summary>
            /// uint32_t ARB_explicit_attrib_location : 1;
            /// uint32_t ARB_explicit_uniform_location : 1;
            /// uint32_t ARB_shading_language_420pack : 1;
            /// uint32_t OVR_multiview : 1;
            /// uint32_t EXT_shader_framebuffer_fetch : 1;
            /// </summary>
            public uint bitfield;
        }

        public struct GLSLCrossDependencyData
        {
            struct GLSLBufferBindPointInfo
            {
                uint slot;
                bool known;
            };

            // A container for a single Vulkan resource binding (<set, binding> pair)
            struct VulkanResourceBinding
            {
                uint set;
                uint binding;
            };

            enum GLSLBufferType
            {
                BufferType_ReadWrite,
                BufferType_Constant,
                BufferType_SSBO,
                BufferType_Texture,
                BufferType_UBO,

                BufferType_Count,
                BufferType_Generic = BufferType_ReadWrite
            };

        }

        [DllImport("hlslcc.dll", CharSet = CharSet.Ansi)]
        public extern static int TranslateHLSL([Out] byte[] bytes);


//[DllImport("hlslcc.dll")]
//        unsafe extern static int TranslateHLSLFromFile(sbyte* filename, uint flags, GLLang language, GlExtensions* extensions,
//            GLSLCrossDependencyData* dependencies,
//            map<std::basic_string<char, std::char_traits<char>,
//                std::allocator<char>>,enum REFLECT_RESOURCE_PRECISION,std::less<std::basic_string<char, std::char_traits<char>, std::allocator<char>>>,std::allocator<std::pair<std::basic_string<char, std::char_traits<char>, std::allocator<char>> const ,enum REFLECT_RESOURCE_PRECISION> > >* samplerPrecisions, HLSLccReflection* reflectionCallbacks, GLSLShader* result)
    }
}
