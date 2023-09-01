using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using CJUtils;
using MADUtils;
using MADShaderUtility;

/// <summary>
/// 
/// </summary>
public class ModelAssetDatabaseMaterialManager : ModelAssetDatabaseTool {

    /// <summary> Distinct tabs separating Manager Functions; </summary>
    public enum SectionType {
        Editor,
        Creator,
        Organizer,
        Replacer
    } /// <summary> Section currently selected in the GUI; </summary>
    public SectionType ActiveSection { get; private set; }

    public class ManagedMaterialData {
        public string path;
        public Material material;

        public ManagedMaterialData(string path, Material material) {
            this.path = path;
            this.material = material;
        }
    }
    public ManagedMaterialData EditedMaterial { get; private set; }

    private GameObject previewTarget;
    private GenericPreview preview;

    private MaterialEditorBundle materialEditor;

    private ModelAssetLibraryAssets customPrefabs;

    private Vector2 editorScroll;

    // Remove
    private static bool abbreviateEditMode;

    #region | Global Methods |

    protected override void InitializeData() {
        if (customPrefabs == null) {
            customPrefabs = ModelAssetLibraryConfigurationCore.ToolAssets;
        } 
    }

    public override void ResetData() {
        CleanMaterialEditor();
        CleanPreview();
    }

    /// <summary>
    /// Dispose of unmanaged resources in the Material Manager;
    /// </summary>
    public override void FlushData() {
        ResetData();
        CleanPreviewTarget();
    }

    public override void SetSelectedAsset(string path) {
        switch (ActiveSection) {
            case SectionType.Editor:
                SetEditedMaterial(path);
                break;
            case SectionType.Replacer:
                break;
        }
    }

    /// <summary>
    /// Sets the GUI's selected Manager Section;
    /// </summary>
    /// <param name="sectionType"> Type of the prospective section to show; </param>
    private void SetSelectedSection(SectionType sectionType) {
        ActiveSection = sectionType;
    }

    #endregion

    #region | Editor Methods |

    /// <summary>
    /// Set the currently edited material data;
    /// </summary>
    /// <param name="path"> Path of the Material to read; </param>
    public void SetEditedMaterial(string path) {
        FlushData();
        SetPreviewTarget(PreviewTarget.Sphere);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        EditedMaterial = new ManagedMaterialData(path, material);
    }

    /// <summary>
    /// Creates a material editor if one is not available;
    /// </summary>
    private void ExtractMaterialEditor() => materialEditor = MaterialEditorBundle.CreateBundle(EditedMaterial.material);

    /// <summary>
    /// A shorthand for drawing the extracted editor;
    /// </summary>
    private void DrawMaterialEditor() => materialEditor.DrawEditor();

    /// <summary>
    /// Clean the material editor;
    /// </summary>
    private void CleanMaterialEditor() => DestroyImmediate(materialEditor);

    #endregion

    #region | Tool GUI |

    /// <summary>
    /// Draws the toolbar for the Material Manager;
    /// </summary>
    public override void DrawToolbar() {
        foreach (SectionType sectionType in System.Enum.GetValues(typeof(SectionType))) {
            DrawMaterialToolbarButton(sectionType);
        }
    }

    /// <summary>
    /// Draws a button on the Material Toolbar;
    /// </summary>
    /// <param name="section"> Section to draw the button for; </param>
    private void DrawMaterialToolbarButton(SectionType section) {
        if (GUILayout.Button(System.Enum.GetName(typeof(SectionType), section), ActiveSection == section
                                       ? UIStyles.SelectedToolbar : EditorStyles.toolbarButton, GUILayout.MinWidth(140), GUILayout.ExpandWidth(true))) {
            SetSelectedSection(section);
        }
    }

    /// <summary>
    /// Select a GUI display based on the currently active section;
    /// </summary>
    public override void ShowGUI() {
        switch (ActiveSection) {
            case SectionType.Editor:
                ShowEditorSection();
                break;
            case SectionType.Creator:
                ShowCreatorSection();
                break;
            case SectionType.Organizer:
                ShowOrganizerSection();
                break;
            case SectionType.Replacer:
                ShowReplacerSection();
                break;
        }
    }

    #region | Editor Section |

    private void ShowEditorSection() {
        if (EditedMaterial == null) {
            EditorUtils.DrawScopeCenteredText("Select a Material from the Hierarchy to begin;");
        } else {
            float panelWidth = 620;
            if (materialEditor == null) ExtractMaterialEditor();
            using (new EditorGUILayout.HorizontalScope()) {
                /// Editor side of the Editor Tab;
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(panelWidth / 2))) {
                    using (new EditorGUILayout.HorizontalScope(UIStyles.WindowBox)) {
                        using (new EditorGUILayout.VerticalScope()) {
                            using (new EditorGUILayout.HorizontalScope(UIStyles.WindowBox)) {
                                using (new EditorGUILayout.HorizontalScope(GUI.skin.box)) {
                                    GUILayout.Label("Material Editor", UIStyles.CenteredLabelBold);
                                }
                            } using (new EditorGUILayout.HorizontalScope(UIStyles.WindowBox)) {
                                GUILayout.Label("Shader:");
                                Rect shaderPosition = EditorGUILayout.GetControlRect(GUILayout.MinWidth(105));
                                GUIContent shaderContent = new GUIContent(EditedMaterial.material.shader == null
                                                                          ? "Missing Shader" : EditedMaterial.material.shader.name);
                                MADShaderUtil.DrawDefaultShaderPopup(shaderPosition, shaderContent, ReplaceMaterialShader);
                                Rect historyPosition = EditorGUILayout.GetControlRect(GUILayout.Width(38));
                                MADShaderUtil.DrawShaderHistoryPopup(historyPosition, ReplaceMaterialShader);
                            } using (new EditorGUILayout.VerticalScope(UIStyles.WindowBox)) {
                                using (var view = new EditorGUILayout.ScrollViewScope(editorScroll)) {
                                    editorScroll = view.scrollPosition;
                                    DrawMaterialEditor();
                                }
                            }
                        }
                    }
                } /// Preview side of the Editor Tab;
                using (new EditorGUILayout.VerticalScope(UIStyles.WindowBox, GUILayout.Width(panelWidth / 2))) {
                    EditorUtils.WindowBoxLabel("Material Preview");
                    DrawMaterialPreview();
                    using (new EditorGUILayout.HorizontalScope(UIStyles.WindowBox)) {
                        DrawMaterialPreviewOptions();
                    }
                }
            }
        }
    }

    private void ReplaceMaterialShader(Shader shader) {
        if (EditedMaterial != null && materialEditor is not null) {
            if (EditedMaterial.material.shader == shader) return;
            materialEditor.editor.SetShader(shader);
        } else Debug.LogWarning("Shader could not be set;");
    }

    private void DrawMaterialPreview() {
        Rect rect = EditorGUILayout.GetControlRect(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        if (preview == null) {
            if (previewTarget != null) {
                preview = GenericPreview.CreatePreview(previewTarget);
            } else EditorUtils.DrawScopeCenteredText("Select a Preview Object");
        } else {
            preview.preview.DrawPreview(rect);
            CleanPreviewTarget();
        }
    }

    private enum PreviewTarget {
        Sphere,
        Cube,
        Other
    } private static PreviewTarget activeTarget;

    private void DrawMaterialPreviewOptions() {
        GUILayout.Label("Preview Object:");
        PreviewTarget selection = (PreviewTarget) EditorGUILayout.EnumPopup(activeTarget);
        if (activeTarget != selection) SetPreviewTarget(selection);
        if (activeTarget == PreviewTarget.Other) {
            GameObject potentialObject = EditorGUILayout.ObjectField(previewTarget, typeof(GameObject), false) as GameObject;
            if (potentialObject != previewTarget) SetPreviewObject(potentialObject);
        }
    }

    private void SetPreviewTarget(PreviewTarget selection) {
        CleanPreview();
        previewTarget = null;
        switch (selection) {
            case PreviewTarget.Sphere:
                SetPreviewObject(customPrefabs.spherePrefab);
                break;
            case PreviewTarget.Cube:
                SetPreviewObject(customPrefabs.cubePrefab);
                break;
        } activeTarget = selection;
    }

    private void SetPreviewObject(GameObject gameObject) {
        previewTarget = Instantiate(gameObject);
        Renderer[] renderers = previewTarget.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers) {
            Material[] nArr = new Material[renderer.sharedMaterials.Length];
            for (int i = 0; i < renderer.sharedMaterials.Length; i++) {
                //nArr[i] = material;
            } //renderer.sharedMaterials = nArr;
        } CleanPreview();
    }

    private void CleanPreview() => DestroyImmediate(preview);

    private void CleanPreviewTarget() => DestroyImmediate(previewTarget);

    #endregion

    private void ShowCreatorSection() {
        if (EditedMaterial == null) {
            EditorUtils.DrawScopeCenteredText("Select a Material from the Hierarchy to begin;");
        } else {
            using (new EditorGUILayout.HorizontalScope()) {
                /// Editor side of the Editor Tab;
                using (new EditorGUILayout.VerticalScope()) {
                    /// Editing mode selection box: Abbreviated/Built-in;
                    using (new EditorGUILayout.HorizontalScope(UIStyles.WindowBox)) {
                        GUILayout.Label("Editing Mode:");
                        if (GUILayout.Button("Abbreviated")) {
                            abbreviateEditMode = true;
                        } if (GUILayout.Button("Built-In")) {
                            abbreviateEditMode = false;
                        }
                    }  /// Selected Editing Mode;
                    using (new EditorGUILayout.HorizontalScope(UIStyles.WindowBox)) {
                        using (new EditorGUILayout.VerticalScope()) {
                            using (new EditorGUILayout.HorizontalScope(UIStyles.WindowBox)) {
                                using (new EditorGUILayout.HorizontalScope(GUI.skin.box)) {
                                    GUILayout.Label("Material Editor", UIStyles.CenteredLabelBold);
                                }
                            } if (abbreviateEditMode) {
                                /// Shader Selection
                                using (new EditorGUILayout.HorizontalScope(UIStyles.WindowBox)) {
                                    GUILayout.Label("Shader:");
                                    /// Shader Popup Function;
                                    /// Shader History Function:
                                    GUILayout.Button(new GUIContent(EditorUtils.FetchIcon("d_UnityEditor.AnimationWindow")));
                                } /// Applied Fields (Based on available fields, the default are those whose keywords are enabled);
                                using (new EditorGUILayout.VerticalScope(UIStyles.WindowBox)) {
                                    using (new EditorGUILayout.ScrollViewScope(Vector2.zero)) {

                                    }
                                } /// Field Selector;
                                using (new EditorGUILayout.VerticalScope(UIStyles.WindowBox)) {
                                    using (new EditorGUILayout.ScrollViewScope(Vector2.zero)) {

                                    }
                                }
                            } else {
                                /// Use Assembly to extract material editor;
                            }
                        }
                    }
                } /// Preview side of the Editor Tab;
                using (new EditorGUILayout.VerticalScope()) {
                    using (new EditorGUILayout.HorizontalScope(UIStyles.WindowBox)) {
                        GUILayout.Label("Material Preview", UIStyles.CenteredLabelBold);
                    }
                }
            }
        }
    }

    private void ShowOrganizerSection() {

    }

    private void ShowReplacerSection() {

    }

    #endregion
}