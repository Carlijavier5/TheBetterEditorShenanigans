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

    private GameObject modelGO;
    private Vector2 materialScroll;

    void OnEnable() {
        ModelAssetLibraryReader.CleanObjectPreview();
        if (Options.model != null) {
            modelGO = AssetDatabase.LoadAssetAtPath<GameObject>(Options.model.assetPath);
        } ProcessLibraryData(Options.model);
        tempMaterials = new Material[MaterialOverrideMap.Count];
    }

    void OnDisable() {
        ModelAssetLibraryReader.CleanObjectPreview();
        FlushImportData();
    }

    void OnGUI() {

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
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
                        Options.materialOverrideMode = MaterialOverrideMode.None;
                    } if (GUILayout.Button("Single", Options.materialOverrideMode == MaterialOverrideMode.Single
                                                     ? UIStyles.ArrangedButtonSelected : GUI.skin.button, GUILayout.MaxWidth(70))) {
                        Options.materialOverrideMode = MaterialOverrideMode.Single;
                    } if (GUILayout.Button("Multiple", Options.materialOverrideMode == MaterialOverrideMode.Multiple
                                                       ? UIStyles.ArrangedButtonSelected : GUI.skin.button, GUILayout.MaxWidth(70))) {
                        Options.materialOverrideMode = MaterialOverrideMode.Multiple;
                    }
                } if (Options.materialOverrideMode == MaterialOverrideMode.Multiple) {
                    using (new EditorGUILayout.HorizontalScope()) {
                        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox, GUILayout.MaxWidth(150))) {
                            GUILayout.Label("Use Single Shader:", GUILayout.MaxWidth(700));
                            Options.useSingleShader = EditorGUILayout.Toggle(Options.useSingleShader);
                        } using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox)) {
                            if (!Options.useSingleShader) GUI.enabled = false;
                            Rect position = EditorGUILayout.GetControlRect();
                            GUIContent shaderContent = new GUIContent(Options.shader == null ? "No Selected Shader" : Options.shader.name);
                            DrawShaderPopup(shaderContent, UpdateGlobalShader);

                            if (!Options.useSingleShader) GUI.enabled = true;
                            //Options.shader = EditorGUILayout.ObjectField(Options.shader, typeof(Shader), false) as Shader;
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
        using (new EditorGUILayout.HorizontalScope(GUI.skin.box)) {
            switch (Options.materialOverrideMode) {
                case MaterialOverrideMode.Single:
                    break;
                case MaterialOverrideMode.Multiple:
                    if (modelGO != null) {
                        using (new EditorGUILayout.VerticalScope(GUI.skin.box)) {
                            using (new EditorGUILayout.VerticalScope(GUI.skin.box)) {
                                GUILayout.Label("Preview", UIStyles.CenteredLabel);
                                ModelAssetLibraryReader.DrawObjectPreviewEditor(modelGO, 96, 112);
                            } using (new EditorGUILayout.VerticalScope(GUI.skin.box, GUILayout.MaxWidth(96))) {
                                EditorUtils.DrawSeparatorLines("New Materials", true);
                                using (new EditorGUILayout.ScrollViewScope(Vector2.zero)) {
                                    DrawNewMaterials();
                                }
                            }
                        } using (new EditorGUILayout.VerticalScope(GUI.skin.box)) {
                            EditorUtils.DrawSeparatorLines("Available Slots", true);
                            using (var view = new EditorGUILayout.ScrollViewScope(materialScroll)) {
                                materialScroll = view.scrollPosition;
                                DrawAvailableSlots();
                            }
                        }
                    } break;
            }
        }
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
        int i = 0;
        foreach (KeyValuePair<string, MaterialData> kvp in MaterialOverrideMap) {
            using (new EditorGUILayout.HorizontalScope(GUI.skin.box)) {
                GUILayout.Label(kvp.Key, UIStyles.CenteredLabelBold);
            } using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox)) {
                    GUILayout.Label("Origin:", UIStyles.ArrangedLabel);
                    if (GUILayout.Button("New", kvp.Value.isNew
                                                   ? UIStyles.ArrangedButtonSelected : GUI.skin.button, GUILayout.MaxWidth(70))) {
                        if (!kvp.Value.isNew) ToggleMaterialMap(kvp.Key);
                    } if (GUILayout.Button("Remap", kvp.Value.isNew
                                                      ? GUI.skin.button : UIStyles.ArrangedButtonSelected, GUILayout.MaxWidth(70))) {
                        if (kvp.Value.isNew) ToggleMaterialMap(kvp.Key);
                    }
                } using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
                    if (kvp.Value.isNew) {
                        using (new EditorGUILayout.HorizontalScope()) {
                            GUILayout.Label("Name:", GUILayout.MaxWidth(46));
                            kvp.Value.name = EditorGUILayout.TextField(kvp.Value.name);
                        } using (new EditorGUILayout.HorizontalScope()) {
                            GUILayout.Label("Albedo:", GUILayout.MaxWidth(46));
                            kvp.Value.albedoMap = (Texture2D) EditorGUILayout.ObjectField(kvp.Value.albedoMap, typeof(Texture2D), false);
                        } using (new EditorGUILayout.HorizontalScope()) {
                            GUILayout.Label("Normal:", GUILayout.MaxWidth(46));
                            kvp.Value.normalMap = (Texture2D) EditorGUILayout.ObjectField(kvp.Value.normalMap, typeof(Texture2D), false);
                        } if (!Options.useSingleShader) {
                            using (new EditorGUILayout.HorizontalScope()) {
                                GUILayout.Label("Shader:", GUILayout.MaxWidth(46));
                                kvp.Value.shader = (Shader) EditorGUILayout.ObjectField(kvp.Value.shader, typeof(Shader), false);
                            }
                        } bool validMaterial = ValidateTemporaryMaterial(kvp.Key);
                        if (!validMaterial) GUI.enabled = false;
                        bool containsKey = TempMaterialMap.ContainsKey(kvp.Key);
                        if (containsKey) {
                            bool materialsAreEqual = ValidateMaterialEquality(kvp.Key);
                            if (materialsAreEqual) GUI.enabled = false;
                            if (GUILayout.Button("Replace Material")) GenerateTemporaryMaterial(kvp.Key);
                            if (materialsAreEqual) GUI.enabled = true;
                        } else {
                            if (GUILayout.Button("Generate Material")) GenerateTemporaryMaterial(kvp.Key);
                        } if (!validMaterial) GUI.enabled = true;
                    } else {
                        using (new EditorGUILayout.HorizontalScope()) {
                            GUILayout.Label("Material:", GUILayout.MaxWidth(52));
                            tempMaterials[i] = (Material) EditorGUILayout.ObjectField(tempMaterials[i], typeof(Material), false);
                            if (tempMaterials[i] != null && tempMaterials[i] != kvp.Value.materialRef) UpdateMaterialRef(kvp.Key, tempMaterials[i]);
                        }
                    }
                }
            } EditorGUILayout.Separator();
            EditorUtils.DrawSeparatorLine(1);
            EditorGUILayout.Separator();
            i++;
        }
    }

    private void DrawShaderPopup(GUIContent shaderContent, System.Action<Shader> method) {
        if (EditorGUI.DropdownButton(position, shaderContent, FocusType.Keyboard, EditorStyles.miniPullDown)) {
            OnShaderResult += method;
            ShowShaderSelectionMagic(position);
        }
    }

    private void UpdateGlobalShader(Shader shader) {
        Options.shader = shader;
    }

    private void UpdateLocalMaterial(Shader shader) {

    }
}