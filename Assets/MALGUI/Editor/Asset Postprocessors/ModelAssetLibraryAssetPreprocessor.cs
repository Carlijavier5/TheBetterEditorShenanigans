using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Reader = ModelAssetLibraryReader;
using TempManager = ModelAssetLibraryTempMaterialManager;

public static class ModelAssetLibraryAssetPreprocessor {

    /// <summary> Different ways to override the materials in the model, if any; </summary>
    public enum MaterialOverrideMode { None, Single, Multiple }

    /// <summary> References and values that govern the reimport process; </summary>
    public class ImportOverrideOptions {
        /// <summary> Reference to the model to be reimported; </summary>
        public ModelImporter model;
        /// <summary> Whether any meshes were found in the imported file; </summary>
        public bool hasMeshes;
        /// <summary> Whether the model should use materials or vertex color; </summary>
        public bool useMaterials;
        /// <summary> Way to replace materials in the model, if the model uses materials; </summary>
        public MaterialOverrideMode materialOverrideMode;
        /// <summary> Whether the material override should use a global shader for all new materials; </summary>
        public bool useSingleShader = true;
        /// <summary> Global shader used for new materials if instructed; </summary>
        public Shader shader;
        /// <summary> Category folder to relocate the model to, if any; </summary>
        public string category;
    } /// <summary> Global Reimport values used to allocate the GUI and the final reimport; </summary>
    public static ImportOverrideOptions Options { get; set; }

    /// <summary> Data used to reference and/or create materials in the Reimport Window; </summary>
    public class MaterialData {
        /// <summary> Whether the slot is being remapped or a new material is being created; </summary>
        public bool isNew;
        /// <summary> Reference to an external, (pre-existing or newly created) material; </summary>
        public Material materialRef;
        /// <summary> Name of the new material, which must abide by the project's convetion; </summary>
        public string name;
        /// <summary> Main texture used to create the material (required); </summary>
        public Texture2D albedoMap;
        /// <summary> Normal map used to create the material; </summary>
        public Texture2D normalMap;
        /// <summary> Shader to base the material on (required if not using a global shader); </summary>
        public Shader shader;
    } /// <summary> Map of identifier keys to the Material Data that has been generated on them; </summary>
    public static Dictionary<string, MaterialData> MaterialOverrideMap { get; set; }

    /// <summary> External key reserved for the Single Material Override Mode; </summary>
    public static string SingleKey { get { return ""; } private set { SingleKey = value; } }

    /// <summary> Map of identifier keys to newly generated materials; </summary>
    public static Dictionary<string, Material> TempMaterialMap { get; private set; }

    /// <summary> Map of identifier keys to New Materials that will ultimately remain in use; </summary>
    public static Dictionary<string, Material> PreservedMaterialMap { get; private set; }

    /// <summary> Original material map used by the Model to reimport; </summary>
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
                    if (string.IsNullOrWhiteSpace(kvp.Key)) continue;
                    if (!kvp.Value.isNew && kvp.Value.materialRef != null) {
                        ReplacePersistentMaterial(kvp.Key, kvp.Value.materialRef, false);
                    } else if (kvp.Value.isNew && TempMaterialMap.ContainsKey(kvp.Key)) {
                        ReplacePersistentMaterial(kvp.Key, TempMaterialMap[kvp.Key], false);
                    } else ReplacePersistentMaterial(kvp.Key, originalInternalMap[kvp.Key], false);
                } Options.model.SaveAndReimport();
                Reader.CleanObjectPreview();
                break;
        } Options.materialOverrideMode = mom;
    }

    public static void UpdateMaterialRef(string key, Material material) {
        bool restore = material == null;
        MaterialOverrideMap[key].materialRef = material;
        if (string.IsNullOrEmpty(key)) ReplaceGlobalMapping(restore ? null : material);
        else ReplacePersistentMaterial(key, restore ? originalInternalMap[key] : material);
    }

    /// <summary>
    /// A very similar version to the one in the Reader;
    /// <br></br> I might just reuse that one eventually, but I made this one for now to save time;
    /// </summary>
    /// <param name="key"> Key of the entry to remap; </param>
    /// <param name="material"> Material to place in the map; </param>
    /// <param name="reimport"> Whether the model should be reimported within this call; </param> 
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
            } serializedObject.ApplyModifiedPropertiesWithoutUndo();
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
    /// Checks whether the given paramaters are enough to generate a material;
    /// </summary>
    /// <param name="key"> Key of the data to pull from the dictionary; </param>
    /// <returns> True if the material data is valid; </returns>
    public static bool ValidateTemporaryMaterial(string key) {
        MaterialData data = MaterialOverrideMap[key];
        if (!ValidateMaterialName(data.name)) return false;
        if (string.IsNullOrWhiteSpace(key)) {
            if (data.shader == null) return false;
        } else {
            if (Options.useSingleShader && Options.shader == null) return false;
            if (!Options.useSingleShader && data.shader == null) return false;
        } if (data.albedoMap == null) return false;
        return true;
    }

    /// <summary>
    /// Check whether the current material data could be used to create a material ditinct from the active one;
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public static bool ValidateMaterialEquality(string key) {
        MaterialData data = MaterialOverrideMap[key];
        Material material = TempMaterialMap[key];
        if (material.mainTexture != data.albedoMap) return false;
        if ((material.IsKeywordEnabled("_NORMALMAP") && material.GetTexture("_BumpMap") != data.normalMap)
            || (!material.IsKeywordEnabled("_NORMALMAP") && data.normalMap != null)) return false;
        if (string.IsNullOrWhiteSpace(key)) {
            if (material.shader != data.shader) return false;
        } else if ( (Options.useSingleShader && material.shader != Options.shader)
                   || (!Options.useSingleShader && material.shader != data.shader) ) {
            return false;
        } return true;
    }

    /// <summary>
    /// Check whether the name abides by the project convention and is a valid file name;
    /// <br></br> Will return false if a material with the same name already exists in the temporary folder;
    /// </summary>
    /// <returns> True if the name is valid; </returns>
    private static bool ValidateMaterialName(string name) {
        return Reader.ValidateFilename(TempManager.TempMaterialPath + "/" + name + ".mat", name) == 0;
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

    /// <summary>
    /// Switches between the New Material and Reference Material modes;
    /// <br></br> If a material replacement has not been defined, it restores the original mapping;
    /// </summary>
    /// <param name="key"></param>
    public static void ToggleMaterialMap(string key) {
        MaterialData data = MaterialOverrideMap[key];
        if (originalInternalMap.ContainsKey(key)) {
            if (data.isNew && data.materialRef != null) {
                ReplacePersistentMaterial(key, data.materialRef);
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
            if (newMaterial == null) ReplacePersistentMaterial(kvp.Key, kvp.Value, false);
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
                if (kvp.Value != null) Object.DestroyImmediate(kvp.Value, true);
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