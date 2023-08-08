using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using CJUtils;
using static ModelAssetLibraryAssetPreprocessor;

public class ModelAssetLibraryAssetPreprocessorGUI : EditorWindow {

    [MenuItem("Assets/Library Reimport", false, 50)]
    public static void LibraryReimport() {
        Options = new ImportOverrideOptions();
        string path = AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs[0]);
        Options.model = AssetImporter.GetAtPath(path) as ModelImporter;
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        Options.hasMeshes = mesh != null;
        Options.useMaterials = Options.hasMeshes ? mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Color) : false;
        Options.category = "None";
        ShowWindow();
    }

    [MenuItem("Assets/Library Reimport", true)]
    private static bool LibraryReimportValidate() {
        return Selection.assetGUIDs.Length == 1
               && AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs[0])) is ModelImporter;
    }

    public static void ShowWindow() {
        var window = GetWindow<ModelAssetLibraryAssetPreprocessorGUI>("Library Reimport");
        window.ShowAuxWindow();
    }

    private Material[] tempMaterials;
    private string shaderKey;

    private GameObject modelGO;

    private Vector2 materialsNewScroll;
    private Vector2 materialSlotScroll;

    void OnEnable() {
        ModelAssetLibraryReader.CleanObjectPreview();
        if (Options == null) return;
        if (Options.model != null) {
            modelGO = AssetDatabase.LoadAssetAtPath<GameObject>(Options.model.assetPath);
        } ProcessLibraryData(Options.model);
        tempMaterials = new Material[MaterialOverrideMap.Count];
    }

    void OnDisable() {
        ModelAssetLibraryReader.CleanObjectPreview();
        if (Options != null) FlushImportData();
    }

    void OnGUI() {
        if (Options == null || Options.model == null) {
            EditorUtils.DrawScopeCenteredText("Unity revolted against this window.\nPlease reload it!") ;
            return;
        } using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
            EditorUtils.DrawSeparatorLines("Core Import Settings", true);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox)) {
                GUILayout.Label("Import Mode:", UIStyles.ArrangedLabel);
                GUI.enabled = Options.hasMeshes;
                GUILayout.Label("Model", UIStyles.ArrangedButtonSelected, GUILayout.MaxWidth(105));
                GUI.enabled = !Options.hasMeshes;
                GUILayout.Label("Animation(s)", GUI.skin.box, GUILayout.MaxWidth(108));
                GUI.enabled = true;
            } using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox)) {
                GUILayout.Label("Category:", UIStyles.ArrangedLabel);
                EditorGUILayout.Popup(0, new string[] { "None", "All" }, GUILayout.MaxWidth(213) );
            } using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox)) {
                GUILayout.Label("Texture Mode:", UIStyles.ArrangedLabel);
                GUI.enabled = Options.hasMeshes;
                if (GUILayout.Button("Material", Options.useMaterials
                                                 ? UIStyles.ArrangedButtonSelected : GUI.skin.button, GUILayout.MaxWidth(105))) {
                    Options.useMaterials = true;
                } if (GUILayout.Button("Vertex Color", Options.useMaterials 
                                                       ? GUI.skin.button : UIStyles.ArrangedButtonSelected, GUILayout.MaxWidth(105))) {
                    Options.useMaterials = false;
                }
            }
        } using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
            EditorUtils.DrawSeparatorLines("Additional Import Settings", true);
            if (Options.useMaterials) {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox)) {
                    GUILayout.Label("Material Override:", UIStyles.ArrangedLabel);
                    if (GUILayout.Button("None", Options.materialOverrideMode == MaterialOverrideMode.None
                                                 ? UIStyles.ArrangedButtonSelected : GUI.skin.button, GUILayout.MaxWidth(70))) {
                        SetMaterialOverrideMode(MaterialOverrideMode.None);
                    } if (GUILayout.Button("Single", Options.materialOverrideMode == MaterialOverrideMode.Single
                                                     ? UIStyles.ArrangedButtonSelected : GUI.skin.button, GUILayout.MaxWidth(70))) {
                        SetMaterialOverrideMode(MaterialOverrideMode.Single);
                    } if (GUILayout.Button("Multiple", Options.materialOverrideMode == MaterialOverrideMode.Multiple
                                                       ? UIStyles.ArrangedButtonSelected : GUI.skin.button, GUILayout.MaxWidth(70))) {
                        SetMaterialOverrideMode(MaterialOverrideMode.Multiple);
                    }
                } if (Options.materialOverrideMode == MaterialOverrideMode.Multiple) {
                    using (new EditorGUILayout.HorizontalScope()) {
                        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox, GUILayout.MaxWidth(150))) {
                            GUILayout.Label("Use Single Shader:", GUILayout.MaxWidth(700));
                            Options.useSingleShader = EditorGUILayout.Toggle(Options.useSingleShader);
                        } using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox)) {
                            if (!Options.useSingleShader) GUI.enabled = false;
                            GUIContent shaderContent = new GUIContent(Options.shader == null ? "No Selected Shader" : Options.shader.name);
                            DrawShaderPopup(shaderContent, null);
                            if (!Options.useSingleShader) GUI.enabled = true;
                        }
                    }
                } DrawMaterialSettings();
            } else {
                using (new EditorGUILayout.VerticalScope(GUI.skin.box)) {
                    GUI.color = new Color(0.9f, 0.9f, 0.9f);
                    GUIStyle nopeStyle = new GUIStyle(UIStyles.CenteredLabelBold);
                    nopeStyle.fontSize--;
                    GUILayout.Label("No Additional Settings", nopeStyle);
                    GUI.color = Color.white;
                }
            }
        }
    }

    private void DrawMaterialSettings() {

        if (modelGO != null) {
            switch (Options.materialOverrideMode) {
                case MaterialOverrideMode.Single:
                    using (new EditorGUILayout.VerticalScope(GUI.skin.box)) {
                        using (new EditorGUILayout.VerticalScope(GUI.skin.box)) {
                            GUILayout.Label("Preview", UIStyles.CenteredLabel);
                            ModelAssetLibraryReader.DrawObjectPreviewEditor(modelGO, 96, 112);
                        }
                    } DrawMaterialSlot(SingleKey, MaterialOverrideMap[SingleKey], 0);
                    break;
                case MaterialOverrideMode.Multiple:
                    using (new EditorGUILayout.HorizontalScope(GUI.skin.box)) {
                        using (new EditorGUILayout.VerticalScope(GUI.skin.box)) {
                            using (new EditorGUILayout.VerticalScope(GUI.skin.box)) {
                                GUILayout.Label("Preview", UIStyles.CenteredLabel);
                                ModelAssetLibraryReader.DrawObjectPreviewEditor(modelGO, 96, 112);
                            } using (new EditorGUILayout.VerticalScope(GUI.skin.box, GUILayout.MaxWidth(96))) {
                                EditorUtils.DrawSeparatorLines("New Materials", true);
                                using (var view = new EditorGUILayout.ScrollViewScope(materialsNewScroll)) {
                                    materialsNewScroll = view.scrollPosition;
                                    DrawNewMaterials();
                                }
                            }
                        } using (new EditorGUILayout.VerticalScope(GUI.skin.box)) {
                            EditorUtils.DrawSeparatorLines("Available Slots", true);
                            using (var view = new EditorGUILayout.ScrollViewScope(materialSlotScroll)) {
                                materialSlotScroll = view.scrollPosition;
                                DrawAvailableSlots();
                            }
                        }
                    } break;
            }
        } else GUILayout.Label("Something went wrong here, ask Carlos or something;", UIStyles.CenteredLabelBold);
    }

    private void DrawNewMaterials() {
        if (TempMaterialMap.Count > 0) {
            foreach (KeyValuePair<string, Material> kvp in TempMaterialMap) {
                using (new EditorGUILayout.VerticalScope(GUI.skin.box)) {
                    GUILayout.Label(kvp.Key, UIStyles.CenteredLabelBold);
                    EditorGUILayout.ObjectField(kvp.Value, typeof(Material), false);
                }
            }
        } else {
            GUILayout.Label("- Empty -", UIStyles.CenteredLabelBold);
            EditorGUILayout.Separator();
        }
    }

    private void DrawAvailableSlots() {
        int i = 1;
        foreach (KeyValuePair<string, MaterialData> kvp in MaterialOverrideMap) {
            if (string.IsNullOrWhiteSpace(kvp.Key)) continue;
            DrawMaterialSlot(kvp.Key, kvp.Value, i);
            EditorGUILayout.Separator();
            EditorUtils.DrawSeparatorLine(1);
            EditorGUILayout.Separator();
            i++;
        }
    }

    private void DrawMaterialSlot(string key, MaterialData data, int i) {
        using (new EditorGUILayout.HorizontalScope(GUI.skin.box)) {
            if (string.IsNullOrEmpty(key)) {
                GUILayout.Label("Global Material", UIStyles.CenteredLabelBold);
            } else GUILayout.Label(key, UIStyles.CenteredLabelBold);
        } using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox)) {
                GUILayout.Label("Origin:", UIStyles.ArrangedLabel);
                if (GUILayout.Button("New", data.isNew
                                                ? UIStyles.ArrangedButtonSelected : GUI.skin.button, GUILayout.MaxWidth(70))) {
                    if (!data.isNew) ToggleMaterialMap(key);
                } if (GUILayout.Button("Remap", data.isNew
                                                    ? GUI.skin.button : UIStyles.ArrangedButtonSelected, GUILayout.MaxWidth(70))) {
                    if (data.isNew) ToggleMaterialMap(key);
                }
            } using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
                if (data.isNew) {
                    using (new EditorGUILayout.HorizontalScope()) {
                        GUILayout.Label("Name:", GUILayout.MaxWidth(46));
                        data.name = EditorGUILayout.TextField(data.name);
                    } using (new EditorGUILayout.HorizontalScope()) {
                        GUILayout.Label("Albedo:", GUILayout.MaxWidth(46));
                        data.albedoMap = (Texture2D) EditorGUILayout.ObjectField(data.albedoMap, typeof(Texture2D), false);
                    } using (new EditorGUILayout.HorizontalScope()) {
                        GUILayout.Label("Normal:", GUILayout.MaxWidth(46));
                        data.normalMap = (Texture2D) EditorGUILayout.ObjectField(data.normalMap, typeof(Texture2D), false);
                    } if (!Options.useSingleShader || Options.materialOverrideMode == MaterialOverrideMode.Single) {
                        using (var scope = new EditorGUILayout.HorizontalScope()) {
                            GUILayout.Label("Shader:", GUILayout.MaxWidth(46));
                            GUIContent shaderContent = new GUIContent(data.shader == null ? "No Shader Selected" : data.shader.name);
                            DrawShaderPopup(shaderContent, key);
                        }
                    } bool validMaterial = ValidateTemporaryMaterial(key);
                    if (!validMaterial) GUI.enabled = false;
                    bool containsKey = TempMaterialMap.ContainsKey(key);
                    if (containsKey) {
                        bool materialsAreEqual = ValidateMaterialEquality(key);
                        if (materialsAreEqual) GUI.enabled = false;
                        using (new EditorGUILayout.HorizontalScope()) {
                            GUI.color = UIColors.Blue;
                            if (GUILayout.Button("Replace")) GenerateTemporaryMaterial(key);
                            if (materialsAreEqual) GUI.enabled = true;
                            GUI.color = UIColors.Red;
                            if (GUILayout.Button("Remove")) RemoveNewMaterial(key);
                        } GUI.color = Color.white;
                    } else {
                        GUI.color = UIColors.Green;
                        if (GUILayout.Button("Generate Material")) GenerateTemporaryMaterial(key);
                        GUI.color = Color.white;
                    } if (!validMaterial) GUI.enabled = true;
                } else {
                    using (new EditorGUILayout.HorizontalScope()) {
                        GUILayout.Label("Material:", GUILayout.MaxWidth(52));
                        tempMaterials[i] = (Material) EditorGUILayout.ObjectField(tempMaterials[i], typeof(Material), false);
                        if (tempMaterials[i] != null && tempMaterials[i] != data.materialRef) UpdateMaterialRef(key, tempMaterials[i]);
                    }
                }
            }
        }
    }

    private void DrawShaderPopup(GUIContent shaderContent, string key) {
        shaderKey = key;
        Rect position = EditorGUILayout.GetControlRect(GUILayout.MinWidth(135));
        if (EditorGUI.DropdownButton(position, shaderContent, FocusType.Keyboard, EditorStyles.miniPullDown)) {
            OnShaderResult += ApplyShaderResult;
            ShowShaderSelectionMagic(position);
        }
    }

    private void ApplyShaderResult(Shader shader) {
        if (shaderKey == null) Options.shader = shader;
        else MaterialOverrideMap[shaderKey].shader = shader;
        shaderKey = null;
        OnShaderResult -= ApplyShaderResult;
    }
}