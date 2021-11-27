//using AssetsTools.NET;
//using AssetsTools.NET.Extra;
//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Text;

//namespace AssetsToolsSandbox
//{
//    class Program
//    {
//        public static void Main(string[] args)
//        {
//            AssetsManager am = new AssetsManager();
//            am.LoadClassPackage("classdata.tpk");

//            AssetsFileInstance fileInst = am.LoadAssetsFile("sharedassets1.assets", true);
//            am.LoadClassDatabaseFromPackage(fileInst.file.typeTree.unityVersion);

//            List<AssetsReplacer> modifiedAssets = new List<AssetsReplacer>();

//            const string assemblyFolderName = "Managed";
//            const string assemblyName = "Assembly-CSharp.dll";
//            const string namespaceName = "";
//            const string className = "RandomThing";

//            int randomThingMsFileId = -1;
//            long randomThingMsPathId = -1;
//            //figure out the pathId of the MonoScript we want to use
//            AssetsFileInstance ggmInst = am.LoadAssetsFile("globalgamemanagers.assets", false);
//            foreach (AssetFileInfoEx scriptInfo in ggmInst.table.GetAssetsOfType((int)AssetClassID.MonoScript))
//            {
//                AssetTypeValueField scriptBf = am.GetTypeInstance(ggmInst.file, scriptInfo).GetBaseField();
//                string scriptClassName = scriptBf.Get("m_ClassName").GetValue().AsString();
//                if (scriptClassName == className)
//                {
//                    randomThingMsPathId = scriptInfo.index;
//                    break;
//                }
//            }

//            //figure out the fileId of the same MonoScript
//            for (int i = 0; i < fileInst.file.dependencies.dependencyCount; i++)
//            {
//                AssetsFileDependency dep = fileInst.file.dependencies.dependencies[i];
//                if (dep.assetPath == "globalgamemanagers.assets")
//                {
//                    randomThingMsFileId = i + 1;
//                    break;
//                }
//            }

//            if (randomThingMsFileId == -1 || randomThingMsPathId == -1)
//            {
//                throw new Exception("oof, couldn't find the MonoScript...");
//            }

//            //path to assembly where we want to create MonoBehaviour from
//            string asmCSAssemblyPath = Path.Combine(Path.GetDirectoryName(fileInst.path), assemblyFolderName, assemblyName);
//            //create template for RandomThing type
//            AssetTypeTemplateField randomThingTemplate = GetMonoBehaviourTemplateField(am, fileInst, namespaceName, className, asmCSAssemblyPath);

//            //get value for RandomThing (allows us to edit the values set by the template)
//            AssetTypeValueField randomThingBf = ValueBuilder.DefaultValueFieldFromTemplate(randomThingTemplate);

//            //tell the new MonoBehaviour how to find the MonoScript
//            AssetTypeValueField m_Script = randomThingBf.Get("m_Script");
//            m_Script.Get("m_FileID").GetValue().Set(randomThingMsFileId);
//            m_Script.Get("m_PathID").GetValue().Set(randomThingMsPathId);

//            //set whatever fields you feel like
//            randomThingBf.Get("sillyNumber").GetValue().Set(69420);

//            //add new asset. I have the PathID set to 1234567 and the MonoID set to 321. make sure the MonoID is unique per class.
//            modifiedAssets.Add(new AssetsReplacerFromMemory(0, 1234567, (int)AssetClassID.MonoBehaviour, 321, randomThingBf.WriteToByteArray()));

//            //create dummy type entry for our new MonoBehaviour
//            Type_0D randomThingBfTypeEntry = new Type_0D()
//            {
//                classId = (int)AssetClassID.MonoBehaviour,
//                unknown16_1 = 0,
//                scriptIndex = 321,
//                typeHash1 = 0,
//                typeHash2 = 0,
//                typeHash3 = 0,
//                typeHash4 = 0,
//                scriptHash1 = 0,
//                scriptHash2 = 0,
//                scriptHash3 = 0,
//                scriptHash4 = 0,
//                typeFieldsExCount = 0,
//                stringTableLen = 0,
//                stringTable = ""
//            };
//            fileInst.file.typeTree.unity5Types.Add(randomThingBfTypeEntry);
            
//            //write new modified assets file
//            using (FileStream fs = File.OpenWrite("sharedassets1.edit.assets"))
//            using (AssetsFileWriter w = new AssetsFileWriter(fs))
//            {
//                fileInst.file.Write(w, 0, modifiedAssets, 0);
//            }
//        }

//        static AssetTypeTemplateField GetMonoBehaviourTemplateField(AssetsManager am, AssetsFileInstance fileInst, string classNamespace, string className, string assemblyPath)
//        {
//            string scriptName;
//            if (classNamespace == "")
//                scriptName = className;
//            else
//                scriptName = classNamespace + "." + className;

//            if (File.Exists(assemblyPath))
//            {
//                MonoDeserializer mc = new MonoDeserializer();
//                mc.Read(scriptName, assemblyPath, fileInst.file.header.format);
//                List<AssetTypeTemplateField> monoTemplateFields = mc.children;

//                AssetTypeTemplateField monoBehaviourTemplate = GetTemplateField(am.classFile, AssetClassID.MonoBehaviour);

//                monoBehaviourTemplate.children = monoBehaviourTemplate.children.Concat(monoTemplateFields).ToArray();
//                monoBehaviourTemplate.childrenCount = monoBehaviourTemplate.children.Length;
//                return monoBehaviourTemplate;
//            }
//            else
//            {
//                throw new FileNotFoundException($"{assemblyPath} does not exist!");
//            }
//        }

//        static AssetTypeTemplateField GetTemplateField(ClassDatabaseFile classFile, AssetClassID classId)
//        {
//            AssetTypeTemplateField templateField = new AssetTypeTemplateField();

//            ClassDatabaseType cldbType = AssetHelper.FindAssetClassByID(classFile, (uint)classId);
//            templateField.FromClassDatabase(classFile, cldbType, 0);

//            return templateField;
//        }
//    }
//}
