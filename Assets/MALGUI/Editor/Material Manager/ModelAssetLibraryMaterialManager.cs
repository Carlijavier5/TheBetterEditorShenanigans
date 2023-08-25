using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using CJUtils;
using ModelReader = ModelAssetLibraryModelReader;

public static class ModelAssetLibraryMaterialManager {

    /// <summary> Distinct tabs separating Manager Functions; </summary>
    public enum SectionType {
        Editor,
        Creator,
        Organizer,
        Replacer
    } /// <summary> Section currently selected in the GUI; </summary>
    public static SectionType ActiveSection { get; private set; }

    public class ManagedMaterialData {
        public string path;
        public Material material;

        public ManagedMaterialData(string path, Material material) {
            this.path = path;
            this.material = material;
        }
    }
    public static ManagedMaterialData EditedMaterial { get; private set; }

    private static GameObject previewObject;

    private static MaterialEditor materialEditor;

    private static List<Shader> shaderHistory;

    private static ModelAssetLibraryAssets customPrefabs;

    // Remove
    private static bool abbreviateEditMode;

    #region | Global Methods |

    public static void FlushAssetData() {
        if (customPrefabs == null) {
            customPrefabs = ModelAssetLibraryConfigurationCore.ToolAssets;
        } CleanMaterialEditor();
    }

    /// <summary>
    /// Sets the GUI's selected Manager Section;
    /// </summary>
    /// <param name="sectionType"> Type of the prospective section to show; </param>
    private static void SetSelectedSection(SectionType sectionType) {
        ActiveSection = sectionType;
    }

    private static void AddToShaderHistory(Shader shader) {
        if (shaderHistory == null) shaderHistory = new List<Shader>();
        if (shaderHistory.Contains(shader)) {
            shaderHistory.Remove(shader);
            shaderHistory.Insert(0, shader);
        } else {
            int maxCount = 5;
            if (shaderHistory.Count >= maxCount) shaderHistory.RemoveAt(maxCount - 1);
            shaderHistory.Insert(0, shader);
        }
    }

    #endregion

    #region | Editor Methods |

    /// <summary>
    /// Set the currently edited material data;
    /// </summary>
    /// <param name="path"> Path of the Material to read; </param>
    public static void SetEditedMaterial(string path) {
        FlushAssetData();
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        EditedMaterial = new ManagedMaterialData(path, material);
    }

    private static void ExtractMaterialEditor() {
        if (materialEditor != null) Object.DestroyImmediate(materialEditor);
        materialEditor = Editor.CreateEditor(EditedMaterial.material, typeof(MaterialEditor)) as MaterialEditor;
    }

    private static void DrawMaterialEditor() {
        materialEditor.serializedObject.Update();
        var changeDetection = typeof(MaterialEditor)
                                .GetMethod("DetectShaderEditorNeedsUpdate", BindingFlags.NonPublic
                                | BindingFlags.Instance);
        changeDetection.Invoke(materialEditor, null);
        MaterialProperty[] properties = MaterialEditor.GetMaterialProperties(new Object[] { EditedMaterial.material });
        EditorGUI.BeginChangeCheck();
        if (materialEditor.customShaderGUI != null) {
            materialEditor.customShaderGUI.OnGUI(materialEditor, properties);
        } else materialEditor.PropertiesDefaultGUI(properties);
        if (EditorGUI.EndChangeCheck()) materialEditor.PropertiesChanged();
    }

    private static void CleanMaterialEditor() => Object.DestroyImmediate(materialEditor);

    #endregion

    /// <summary>
    /// Draws the toolbar for the Material Manager;
    /// </summary>
    public static void DrawMaterialToolbar() {
        foreach (SectionType sectionType in System.Enum.GetValues(typeof(SectionType))) {
            DrawMaterialToolbarButton(sectionType);
        }
    }

    /// <summary>
    /// Draws a button on the Material Toolbar;
    /// </summary>
    /// <param name="section"> Section to draw the button for; </param>
    private static void DrawMaterialToolbarButton(SectionType section) {
        if (GUILayout.Button(System.Enum.GetName(typeof(SectionType), section), ActiveSection == section
                                       ? UIStyles.SelectedToolbar : EditorStyles.toolbarButton, GUILayout.MinWidth(140), GUILayout.ExpandWidth(true))) {
            SetSelectedSection(section);
        }
    }

    /// <summary>
    /// Select a GUI display based on the currently active section;
    /// </summary>
    public static void ShowSelectedSection() {
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

    #region | Shader Selection Shenanigans |

    /// <summary> Delegate for the local Shader Popup event set-up; </summary>
    public static System.Action<Shader> OnShaderResult;

    /// <summary>
    /// Fetches the internal Advanced Popup used for shader selection in the Material Editor;
    /// </summary>
    /// <param name="position"> Rect used to draw the popup button; </param>
    public static void ShowShaderSelectionAdvancedDropdown(Rect position) {
        System.Type type = typeof(Editor).Assembly.GetType("UnityEditor.MaterialEditor+ShaderSelectionDropdown");
        var dropDown = System.Activator.CreateInstance(type, args: new object[] { Shader.Find("Transparent/Diffuse"), (System.Action<object>) OnSelectedShaderPopup });
        MethodInfo method = type.GetMethod("Show");
        method.Invoke(dropDown, new object[] { position });
    }

    /// <summary>
    /// Output method for the Shader Selection event set-up;
    /// </summary>
    /// <param name="objShaderName"> Object output from the Shader Selection event containing a shader name; </param>
    private static void OnSelectedShaderPopup(object objShaderName) {
        var shaderName = (string) objShaderName;
        if (!string.IsNullOrEmpty(shaderName)) {
            OnShaderResult?.Invoke(Shader.Find(shaderName));
        }
    }

    /// <summary>
    /// Draws a standard shader popup at the given Rect;
    /// </summary>
    /// <param name="position"> Rect where the popup will be drawn; </param>
    /// <param name="shaderContent"> Text displayed on the popup button; </param>
    /// <param name="shaderCallback"> Callback subscribing to the OnShaderResult event;
    /// <br></br> NOTE: The callback should unsubscribe from the event to avoid unexpected behavior; </param>
    public static void DrawDefaultShaderPopup(Rect position, GUIContent shaderContent, System.Action<Shader> shaderCallback) {
        if (EditorGUI.DropdownButton(position, shaderContent, FocusType.Keyboard, EditorStyles.miniPullDown)) {
            ShowShaderSelectionAdvancedDropdown(position);
            OnShaderResult += shaderCallback;
        }
    }

    private static void DrawShaderHistoryPopup(Rect position, System.Action<Shader> callback) {
        if (EditorGUI.DropdownButton(position, new GUIContent(EditorUtils.FetchIcon("d_UnityEditor.AnimationWindow")),
                                     FocusType.Keyboard, EditorStyles.miniPullDown)) {
            ShowShaderHistoryAdvancedDropdown(position);
            OnShaderResult += callback;
        }
    }

    private static void ShowShaderHistoryAdvancedDropdown(Rect rect) {
        ShaderHistoryAdvancedDropdown historyDropdown = new ShaderHistoryAdvancedDropdown(shaderHistory, OnSelectedShaderPopup);
        historyDropdown.Show(rect);
    }

    private class ShaderHistoryAdvancedDropdown : AdvancedDropdown {

        private System.Action<object> onSelectedShaderPopup;
        private List<Shader> shaderList;

        public ShaderHistoryAdvancedDropdown(List<Shader> shaderList, System.Action<object> onSelectedShaderPopup)
                : base(new AdvancedDropdownState()) {
            minimumSize = new Vector2(150, 0);
            this.shaderList = shaderList;
            this.onSelectedShaderPopup = onSelectedShaderPopup;
        }

        protected override AdvancedDropdownItem BuildRoot() {
            AdvancedDropdownItem root;
            if (shaderList == null || shaderList.Count == 0) {
                root = new AdvancedDropdownItem("No Recent Shaders");
            } else {
                root = new AdvancedDropdownItem("Recent Shaders");
                foreach (Shader shader in shaderList) {
                    root.AddChild(new AdvancedDropdownItem(shader.name));
                }
            }
            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item) {
            onSelectedShaderPopup(item.name);
        }
    }

    #endregion

    #region | Editor Section |

    private static void ShowEditorSection() {
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
                                DrawDefaultShaderPopup(shaderPosition, shaderContent, ReplaceMaterialShader);
                                Rect historyPosition = EditorGUILayout.GetControlRect(GUILayout.Width(38));
                                DrawShaderHistoryPopup(historyPosition, ReplaceMaterialShader);
                            } using (new EditorGUILayout.VerticalScope(UIStyles.WindowBox)) {
                                using (new EditorGUILayout.ScrollViewScope(Vector2.zero)) {
                                    DrawMaterialEditor();
                                }
                            }
                        }
                    }
                } /// Preview side of the Editor Tab;
                using (new EditorGUILayout.VerticalScope(UIStyles.WindowBox, GUILayout.Width(panelWidth / 2))) {
                    EditorUtils.DrawWindowBoxLabel("Material Preview");
                    DrawMaterialPreview();
                    using (new EditorGUILayout.HorizontalScope(UIStyles.WindowBox)) {
                        DrawMaterialPreviewOptions();
                    }
                }
            }
        }
    }

    private static void ReplaceMaterialShader(Shader shader) {
        if (EditedMaterial != null && materialEditor != null) {
            if (EditedMaterial.material.shader == shader) return;
            materialEditor.SetShader(shader);
            AddToShaderHistory(shader);
        } else Debug.LogWarning("Shader could not be set;");
    }

    private static void DrawMaterialPreview() {
        if (previewObject != null) {
            ModelReader.DrawObjectPreviewEditor(previewObject, 1, 1);
        } else EditorUtils.DrawScopeCenteredText("Select a Preview Object to display here;");
    }

    private enum PreviewTarget {
        Sphere,
        Cube,
        Other
    } private static PreviewTarget activeTarget;

    private static void DrawMaterialPreviewOptions() {
        GUILayout.Label("Preview Object:");
        PreviewTarget selection = (PreviewTarget) EditorGUILayout.EnumPopup(activeTarget);
        if (activeTarget != selection) SetPreviewTarget(selection);
        if (activeTarget == PreviewTarget.Other) {
            GameObject potentialObject = EditorGUILayout.ObjectField(previewObject, typeof(GameObject), false) as GameObject;
            if (potentialObject != previewObject) SetPreviewObject(potentialObject);
        }
    }

    private static void SetPreviewTarget(PreviewTarget selection) {
        previewObject = null;
        switch (selection) {
            case PreviewTarget.Sphere:
                SetPreviewObject(customPrefabs.spherePrefab);
                break;
            case PreviewTarget.Cube:
                SetPreviewObject(customPrefabs.cubePrefab);
                break;
        } activeTarget = selection;
    }

    private static void SetPreviewObject(GameObject gameObject) {
        previewObject = Object.Instantiate(gameObject);
        Renderer[] renderers = previewObject.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers) {
            Material[] nArr = new Material[renderer.sharedMaterials.Length];
            for (int i = 0; i < renderer.sharedMaterials.Length; i++) {
                //nArr[i] = material;
            } //renderer.sharedMaterials = nArr;
        } ModelReader.CleanObjectPreview();
    }

    #endregion

    private static void ShowCreatorSection() {
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

    private static void ShowOrganizerSection() {

    }

    private static void ShowReplacerSection() {

    }
}