using AssetsTools.NET;
using AssetsTools.NET.Extra;
using BundleKit.Assets;
using BundleKit.Assets.ShaderData;
using BundleKit.Utility;
using System.IO;
using System.Linq;
using ThunderKit.Core.Data;
using UnityEditor;
using UnityEngine;
using System;
using System.Runtime.InteropServices;

public class DecompileShader
{
    [MenuItem("Redux/DecompileShader")]
    public static void Decompile()
    {
        Debug.Log("running shader import");
        var settings = ThunderKitSetting.GetOrCreateSettings<ThunderKitSettings>();
        var gameName = Path.GetFileNameWithoutExtension(settings.GameExecutable);
        var dataDirectoryPath = Path.Combine(settings.GamePath, $"{gameName}_Data");
        var bundleDirectory = Path.Combine(dataDirectoryPath, "StreamingAssets", "aa", "StandaloneWindows64");
        var bundlePath = Path.Combine(bundleDirectory, "sharedshaders_assets_all.bundle");
        var classDataPath = Path.Combine("Packages", "com.passivepicasso.bundlekit", "Library", "classdata.tpk");

        var am = new AssetsManager();
        am.LoadClassPackage(classDataPath);
        am.LoadClassDatabaseFromPackage(Application.unityVersion);

        var sourceFiles = new string[]
        {
            Path.Combine(dataDirectoryPath, "globalgamemanagers"),
            Path.Combine(dataDirectoryPath, "globalgamemanagers.assets"),
            Path.Combine(dataDirectoryPath, "resources.assets"),
        }.Concat(Directory.EnumerateFiles(dataDirectoryPath, "sharedassets*.assets"));
        foreach (var sourceAssetsFile in sourceFiles)
        {
            am.LoadAssetsFile(sourceAssetsFile, true);
        }
        //foreach (var otherBundle in Directory.EnumerateFiles(bundleDirectory, "*.bundle"))
        //{
        //    var otherBi = am.LoadBundleFile(otherBundle, true);
        //    am.LoadAssetsFileFromBundle(otherBi, 0, false);
        //}
        var depBundle = am.LoadBundleFile(Path.Combine(bundleDirectory, "dd9d3e75d7c481fb62ac5d00e2dd6d15_unitybuiltinshaders.bundle"), true);
        am.LoadAssetsFileFromBundle(depBundle, 0, false);

        BundleFileInstance bundle = am.LoadBundleFile(bundlePath, true);
        AssetsFileInstance assetsFileInst = am.LoadAssetsFileFromBundle(bundle, 0, true);

        var resourceManagerDb = new ResourceManagerDb(am);
        var shaderInfos = assetsFileInst.file.GetAssetsOfType((int)AssetClassID.Shader);

        foreach (var thing in shaderInfos)
        {
            AssetTree tree;
            tree = assetsFileInst.GetHierarchy(am, resourceManagerDb, 0, thing.PathId);
            var name = tree.name;
            var baseField = tree.sourceData.baseField;

            //if(!name.Contains("CelestialBody_Local"))
            //{
            //    continue;
            //}

            Debug.Log(name);

            var blob = new ShaderBlob(baseField);
            // enumerate shader variants and extract
            var subShaders = baseField["m_ParsedForm"]["m_SubShaders"].Select(s => s.Children[0]);
            foreach (var subShader in subShaders)
            {
                Debug.Assert(subShader.TypeName == "SerializedSubShader");
                var passes = subShader["m_Passes"].Children[0];
                foreach (var passData in passes)
                {
                    Debug.Assert(passData.TypeName == "SerializedPass");
                    Debug.Log("Translating " + name);


                    string normalized = name.Replace(Path.AltDirectorySeparatorChar, '_');
                    ExtractProgram(passData["progVertex"], blob, "shader_output/" + normalized + "_vertex_");
                    ExtractProgram(passData["progFragment"], blob, "shader_output/" + normalized + "_frag_");
                }
            }
        }
    }

    private static void ExtractProgram(AssetTypeValueField field, ShaderBlob blob, string name)
    {
        Debug.Assert(field.TypeName == "SerializedProgram");
        // contains:
        // vector m_SubPrograms (usually empty)
        // vector m_PlayerSubPrograms  
        // vector m_ParameterblobIndicies
        // SerializedProgramParameters m_CommonParameters
        // vector m_SerializedKeywordStateMask

        // m_PlayerSubPrograms is an array of arrays.  The outer array seems to be length 1. (purpose unknown)
        // inner arrays is an array of association between keywords and the blob index.  May be empty (due to variant stripping?)
        var subPrograms = field["m_PlayerSubPrograms"].Children[0].SelectMany(c => c.Children[0].Children);

        int index = 0;
        foreach (var subProgram in subPrograms)
        {
            Debug.Assert(subProgram.TypeName == "SerializedPlayerSubProgram");
            uint blobIndex = subProgram["m_BlobIndex"].AsUInt;
            var variant = blob.ExtractVariantAtIndex((int)blobIndex);

            var input = variant.shaderObj;
            var resultPtr = D2G.decompile_to_hlsl(input, (UIntPtr)input.Length);

            if (resultPtr == IntPtr.Zero)
            {
                Debug.LogError("Failed to compile shader" + name);
                File.Create(name + index + ".dxbc").Dispose();
                File.WriteAllBytes(name + index + ".dxbc", input);
                continue;
            }
            
            var result = Marshal.PtrToStringAnsi(resultPtr);

            var outputPath = name + index + ".hlsl";
            File.Create(outputPath).Dispose();
            File.WriteAllText(outputPath, result);
            index++;
        }
    }
}
