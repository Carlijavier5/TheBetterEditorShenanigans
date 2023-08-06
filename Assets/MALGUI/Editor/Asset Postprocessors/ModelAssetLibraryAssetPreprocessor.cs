using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Reader = ModelAssetLibraryReader;

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

    public static Dictionary<string, Material> TempMaterialMap { get; private set; }

    private static Dictionary<string, Material> originalInternalMap;

    public static System.Action<Shader> OnShaderResult;

    public static void UpdateMaterialRef(string key, Material material) {
        MaterialOverrideMap[key].materialRef = material;
        ReplacePersistentMaterial(key, material);
    }

    /// <summary>
    /// A very similar version to the one in the Reader;
    /// <br></br> I might just reuse that one eventually, but I made this one for now to save time;
    /// </summary>
    /// <param name="key"> Key of the entry to remap; </param>
    /// <param name="material"> Material to place in the map; </param>
    private static void ReplacePersistentMaterial(string key, Material material) {
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
        } Options.model.SaveAndReimport();
        Reader.CleanObjectPreview();
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
        } TempMaterialMap = new Dictionary<string, Material>();
    }

    /// <summary>
    /// Checks whether the given paramaters to generate 
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public static bool ValidateTemporaryMaterial(string key) {
        MaterialData data = MaterialOverrideMap[key];
        if (Options.useSingleShader && Options.shader == null) return false;
        if (!Options.useSingleShader && data.shader == null) return false;
        if (data.albedoMap == null) return false;
        return true;
    }

    public static void GenerateTemporaryMaterial(string key) {
        MaterialData data = MaterialOverrideMap[key];
        Material newMaterial = new Material(Options.useSingleShader ? Options.shader : data.shader);
        newMaterial.hideFlags = HideFlags.DontSave;
        newMaterial.mainTexture = data.albedoMap;
        if (data.normalMap != null) {
            newMaterial.EnableKeyword("_NORMALMAP");
            newMaterial.SetTexture("_BumpMap", data.normalMap);
        } if (TempMaterialMap.ContainsKey(key)) Object.DestroyImmediate(TempMaterialMap[key]);
        TempMaterialMap[key] = newMaterial;
        ReplacePersistentMaterial(key, newMaterial);
    }

    public static bool ValidateMaterialEquality(string key) {
        MaterialData data = MaterialOverrideMap[key];
        Material material = TempMaterialMap[key];
        if (material.mainTexture != data.albedoMap) return false;
        if ( (material.IsKeywordEnabled("_NORMALMAP") && material.GetTexture("_BumpMap") != data.normalMap)
            || (!material.IsKeywordEnabled("_NORMALMAP") && data.normalMap != null) ) return false;
        if (!Options.useSingleShader
            && material.shader != data.shader) return false;
        return true;
    }

    public static void ToggleMaterialMap(string key) {
        MaterialData data = MaterialOverrideMap[key];
        if (data.isNew && data.materialRef != null) {
            if (TempMaterialMap.ContainsKey(key)) ReplacePersistentMaterial(key, data.materialRef);
        } else if (!data.isNew && TempMaterialMap.ContainsKey(key)) ReplacePersistentMaterial(key, TempMaterialMap[key]);
        else ReplacePersistentMaterial(key, originalInternalMap[key]);
        data.isNew = !data.isNew;
    }

    public static void PublishExtData() {
        
    }

    public static void RelocateModelAsset() {

    }

    public static void FlushImportData() {
        MaterialOverrideMap = null;
        if (TempMaterialMap != null) {
            foreach (KeyValuePair<string, Material> kvp in TempMaterialMap) {
                if (kvp.Value != null) Object.DestroyImmediate(kvp.Value);
            } TempMaterialMap = null;
        } Options = null;
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