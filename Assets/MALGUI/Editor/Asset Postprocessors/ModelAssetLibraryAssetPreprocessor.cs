using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Reader = ModelAssetLibraryReader;
using TempManager = ModelAssetLibraryTempMaterialManager;

public static class ModelAssetLibraryAssetPreprocessor {

    public enum MaterialOverrideMode { None, Single, Multiple }

    public class ImportOverrideOptions {
        public ModelImporter model;
        public bool hasMeshes;
        public bool useMaterials;
        public MaterialOverrideMode materialOverrideMode;
        public bool useSingleShader = true;
        public Shader shader;
        public string category;
    } public static ImportOverrideOptions Options { get; set; }

    public class MaterialData {
        public bool isNew;
        public Material materialRef;
        public string name;
        public Texture2D albedoMap;
        public Texture2D normalMap;
        public Shader shader;
    } public static Dictionary<string, MaterialData> MaterialOverrideMap { get; set; }

    public static string SingleKey { get { return ""; } private set { SingleKey = value; } }

    public static Dictionary<string, Material> TempMaterialMap { get; private set; }

    private static Dictionary<string, Material> originalInternalMap;

    public static System.Action<Shader> OnShaderResult;

    public static void SetMaterialOverrideMode(MaterialOverrideMode mom) {
        switch (mom) {
            case MaterialOverrideMode.Single:
                if (MaterialOverrideMap[SingleKey].isNew) {
                    if (TempMaterialMap.ContainsKey(SingleKey)) {
                        ReplaceGlobalMapping(TempMaterialMap[SingleKey]);
                    } else ReplaceGlobalMapping();
                } else {
                    if (MaterialOverrideMap[SingleKey].materialRef != null) {
                        ReplaceGlobalMapping(MaterialOverrideMap[SingleKey].materialRef);
                    } else ReplaceGlobalMapping();
                } break;
            case MaterialOverrideMode.Multiple:
                foreach (KeyValuePair<string, MaterialData> kvp in MaterialOverrideMap) {
                    
                } break;
        } Options.materialOverrideMode = mom;
    }

    public static void UpdateMaterialRef(string key, Material material) {
        bool restore = material == null;
        MaterialOverrideMap[key].materialRef = material;
        if (string.IsNullOrEmpty(key)) ReplaceGlobalMapping(restore ? null : material);
        else ReplacePersistentMaterial(key, restore ? material : originalInternalMap[key]);
    }

    /// <summary>
    /// A very similar version to the one in the Reader;
    /// <br></br> I might just reuse that one eventually, but I made this one for now to save time;
    /// </summary>
    /// <param name="key"> Key of the entry to remap; </param>
    /// <param name="material"> Material to place in the map; </param>
    /// <param name="reimportOnGlobal"> Whether the model should be reimported at the end; </param> 
    private static void ReplacePersistentMaterial(string key, Material material, bool reimport = true) {
        using (SerializedObject serializedObject = new SerializedObject(Options.model)) {
            SerializedProperty extObjects = serializedObject.FindProperty("m_ExternalObjects");
            int size = extObjects.arraySize;

            for (int i = 0; i < size; i++) {
                SerializedProperty extObject = extObjects.GetArrayElementAtIndex(i);
                if (extObject.FindPropertyRelative("first.name").stringValue == key) {
                    extObject.FindPropertyRelative("second").objectReferenceValue = material;
                    break;
                }
            } serializedObject.ApplyModifiedProperties();
        } if (reimport) {
            Options.model.SaveAndReimport();
            Reader.CleanObjectPreview();
        }
    }

    /// <summary>
    /// Replicate the internal reference map of the Importer with potential override data;
    /// </summary>
    /// <param name="model"> Model to reimport; </param>
    public static void ProcessLibraryData(ModelImporter model) {
        originalInternalMap = Reader.LoadInternalMaterialMap(model);
        MaterialOverrideMap = new Dictionary<string, MaterialData>();
        foreach (KeyValuePair<string, Material> kvp in originalInternalMap) {
            MaterialOverrideMap[kvp.Key] = new MaterialData() { materialRef = kvp.Value, name = kvp.Key };
        } MaterialOverrideMap[SingleKey] = new MaterialData() { name = SingleKey };
        TempMaterialMap = new Dictionary<string, Material>();
    }

    /// <summary>
    /// Checks whether the given paramaters to generate 
    /// </summary>
    /// <param name="data"> Data to manipulate; </param>
    /// <returns> True if the material is valid; </returns>
    public static bool ValidateTemporaryMaterial(string key) {
        MaterialData data = MaterialOverrideMap[key];
        if (Options.useSingleShader && Options.shader == null) return false;
        if (!Options.useSingleShader && data.shader == null) return false;
        if (data.albedoMap == null) return false;
        return true;
    }

    public static void GenerateTemporaryMaterial(string key) {
        MaterialData data = MaterialOverrideMap[key];
        Material newMaterial;
        if (Options.materialOverrideMode == MaterialOverrideMode.Single) {
            newMaterial = new Material(data.shader);
        } else newMaterial = new Material(Options.useSingleShader ? Options.shader : data.shader);
        newMaterial.name = data.name;
        newMaterial.mainTexture = data.albedoMap;
        if (data.normalMap != null) {
            newMaterial.EnableKeyword("_NORMALMAP");
            newMaterial.SetTexture("_BumpMap", data.normalMap);
        } if (TempMaterialMap.ContainsKey(key)) RemoveNewMaterial(key);
        TempManager.CreateTemporaryMaterialAsset(newMaterial);
        TempMaterialMap[key] = newMaterial;
        if (string.IsNullOrEmpty(key)) ReplaceGlobalMapping(newMaterial);
        else ReplacePersistentMaterial(key, newMaterial);
    }

    public static bool ValidateMaterialEquality(string key) {
        MaterialData data = MaterialOverrideMap[key];
        Material material = TempMaterialMap[key];
        if (material.mainTexture != data.albedoMap) return false;
        if ((material.IsKeywordEnabled("_NORMALMAP") && material.GetTexture("_BumpMap") != data.normalMap)
            || (!material.IsKeywordEnabled("_NORMALMAP") && data.normalMap != null)) return false;
        if (!Options.useSingleShader
            && material.shader != data.shader) return false;
        return true;
    }

    /// <summary>
    /// Switches between the New Material and Reference Material modes;
    /// <br></br> If a material replacement has not been defined, it restores the original mapping;
    /// </summary>
    /// <param name="key"></param>
    public static void ToggleMaterialMap(string key) {
        MaterialData data = MaterialOverrideMap[key];
        if (originalInternalMap.ContainsKey(key)) {
            if (data.isNew && data.materialRef != null) {
                if (TempMaterialMap.ContainsKey(key)) ReplacePersistentMaterial(key, data.materialRef);
            } else if (!data.isNew && TempMaterialMap.ContainsKey(key)) ReplacePersistentMaterial(key, TempMaterialMap[key]);
            else ReplacePersistentMaterial(key, originalInternalMap[key]);
        } else {
            if (data.isNew && data.materialRef != null) {
                if (TempMaterialMap.ContainsKey(key)) ReplaceGlobalMapping(data.materialRef);
            } else if (!data.isNew && TempMaterialMap.ContainsKey(key)) ReplaceGlobalMapping(TempMaterialMap[key]);
            else ReplaceGlobalMapping();
        } data.isNew = !data.isNew;
    }

    public static void ReplaceGlobalMapping(Material newMaterial = null) {
        foreach (KeyValuePair<string, Material> kvp in originalInternalMap) {
            if (newMaterial == null) ReplacePersistentMaterial(kvp.Key, kvp.Value);
            else ReplacePersistentMaterial(kvp.Key, newMaterial, false);
        } Options.model.SaveAndReimport();
        Reader.CleanObjectPreview();
    }

    public static void RemoveNewMaterial(string key, bool restore = false) {
        TempManager.CleanMaterial(TempMaterialMap[key]);
        TempMaterialMap.Remove(key);
        if (restore) {
            if (originalInternalMap.ContainsKey(key)) ReplacePersistentMaterial(key, originalInternalMap[key]);
            else ReplaceGlobalMapping();
        }
    }

    public static void PublishExtData() {
        
    }

    public static void RelocateModelAsset() {

    }

    public static void FlushImportData() {
        if (TempMaterialMap != null) {
            foreach (KeyValuePair<string, Material> kvp in TempMaterialMap) {
                if (kvp.Value != null) Object.DestroyImmediate(kvp.Value);
            } TempManager.CleanAllMaterials();
            TempMaterialMap = null;
        } ReplaceGlobalMapping();
        Options = null; 
        MaterialOverrideMap = null;
        originalInternalMap = null;
    }

    public static void ShowShaderSelectionMagic(Rect position) {
        System.Type type = typeof(Editor).Assembly.GetType("UnityEditor.MaterialEditor+ShaderSelectionDropdown");
        var dropDown = System.Activator.CreateInstance(type, args: new object[] { Shader.Find("Transparent/Diffuse"), (System.Action<object>) OnSelectedShaderPopup });
        MethodInfo method = type.GetMethod("Show");
        method.Invoke(dropDown, new object[] { position });
    }

    private static void OnSelectedShaderPopup(object objShaderName) {
        var shaderName = (string) objShaderName;
        if (!string.IsNullOrEmpty(shaderName)) {
            OnShaderResult?.Invoke(Shader.Find(shaderName));
        }
    }
}