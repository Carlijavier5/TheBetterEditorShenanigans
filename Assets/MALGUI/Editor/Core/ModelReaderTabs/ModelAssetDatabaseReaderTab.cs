using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using MADUtils;
using CJUtils;
using static ModelAssetDatabaseReader;
using ExtData = ModelAssetLibraryExtData;
using static ModelAssetDatabaseGUI;

public abstract class ModelAssetDatabaseReaderTab : Object {

    /// <summary> Every tab will be managed by a parent tool, and will have a handy reference to it; </summary>
    protected readonly ModelAssetDatabaseReader Reader;

    /// <summary>
    /// Initialize base tab data when constructing the tab;
    /// </summary>
    public ModelAssetDatabaseReaderTab(ModelAssetDatabaseReader Reader) { this.Reader = Reader; }

    /// <summary>
    /// Load the corresponding Model when selecting the tab 
    /// </summary>
    public virtual void LoadData(string path) { }

    /// <summary>
    /// Reset tab dependent data when abandoning the tab;
    /// </summary>
    public virtual void ResetData() { }

    /// <summary>
    /// Flush all unmanaged data when the tab is destroyed;
    /// </summary>
    public virtual void FlushData() { }



    public abstract void ShowGUI();
}

public class ModelAssetDatabaseReaderTabModel : ModelAssetDatabaseReaderTab {

    /// <summary> String to display on property undoes; </summary>
    private const string UNDO_PROPERTY = "Model Importer Property Change";
    private ModelImporter Model { get { return Reader.Model; } }
    /// <summary> Directory information on the target file; </summary>
    private FileInfo FileInfo;

    /// <summary> Temporary variable storing potential asset notes; </summary>
    private static bool editNotes;
    /// <summary> Temporary notes stored in the GUI; </summary>
    private static string notes;

    private static Vector2 noteScroll;

    public ModelAssetDatabaseReaderTabModel(ModelAssetDatabaseReader Reader) : base(Reader) { }

    public override void LoadData(string path) {
        FileInfo = new FileInfo(path);
    }

    public override void ResetData() {
        Reader.CleanObjectPreview();
        notes = null;
        editNotes = false;
    }

    public override void FlushData() => Reader.CleanObjectPreview();

    /// <summary>
    /// Updates the Model Notes and disables hot control to properly update the Text Area;
    /// </summary>
    /// <param name="notes"> Notes to pass to the ExtData; </param>
    private void UpdateAssetNotes(string notes) {
        using (var so = new SerializedObject(Reader.ModelExtData)) {
            SerializedProperty noteProperty = so.FindProperty("notes");
            noteProperty.stringValue = notes;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        GUIUtility.keyboardControl = 0;
        GUIUtility.hotControl = 0;
    }

    /// <summary> GUI Display for the Model Section </summary>
    public override void ShowGUI() {

        using (new EditorGUILayout.HorizontalScope()) {
            /// Model Preview + Model Details;
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(200), GUILayout.Height(200))) {
                using (new EditorGUILayout.VerticalScope(GUI.skin.box, GUILayout.Width(192), GUILayout.Height(192))) {
                    GUILayout.Label("Model Preview", UIStyles.CenteredLabel);
                    Reader.DrawObjectPreviewEditor(Reader.RootPrefab, 192, 192);

                    using (new EditorGUILayout.HorizontalScope()) {
                        GUILayout.FlexibleSpace();
                        using (new EditorGUILayout.VerticalScope(GUI.skin.box, GUILayout.Width(196), GUILayout.Height(100))) {
                            GUILayout.Label("Model Details", UIStyles.CenteredLabel);
                            GUILayout.FlexibleSpace();
                            EditorUtils.DrawLabelPair("Vertex Count:", Reader.GlobalVertexCount.ToString());
                            EditorUtils.DrawLabelPair("Triangle Count: ", Reader.GlobalTriangleCount.ToString());
                            EditorUtils.DrawLabelPair("Mesh Count: ", Reader.MeshRenderers.Count.ToString());
                            EditorUtils.DrawLabelPair("Rigged: ", Model.avatarSetup == 0 ? "No" : "Yes");
                        } GUILayout.FlexibleSpace();
                    }
                }
            }
            /// Model Data;
            using (var view = new EditorGUILayout.VerticalScope(GUILayout.MaxWidth(ModelAssetDatabaseReader.PANEL_WIDTH))) {

                EditorUtils.DrawSeparatorLines("External File Info", true);
                using (new EditorGUILayout.HorizontalScope()) {
                    GUILayout.Label("File Path:", new GUIStyle(GUI.skin.label) { contentOffset = new Vector2(0, 1) }, GUILayout.MaxWidth(55));
                    GUIStyle pathStyle = new GUIStyle(EditorStyles.textField) { margin = new RectOffset(0, 0, 3, 2) };
                    EditorGUILayout.SelectableLabel(Model.assetPath, pathStyle, GUILayout.MaxWidth(260), GUILayout.MaxHeight(19));
                    GUIContent content = new GUIContent("  Open Folder", EditorUtils.FetchIcon("Profiler.Open"));
                    if (GUILayout.Button(content, UIStyles.TextureButton, GUILayout.MinWidth(120), GUILayout.Height(20))) {
                        EditorUtility.RevealInFinder(Model.assetPath);
                    }
                }

                using (new EditorGUILayout.HorizontalScope()) {
                    GUIStyle extStyle = new GUIStyle(GUI.skin.label) { contentOffset = new Vector2(0, 2) };
                    EditorUtils.DrawLabelPair("File Size:", EditorUtils.ProcessFileSize(FileInfo.Length), extStyle, GUILayout.MaxWidth(115));
                    GUILayout.FlexibleSpace();
                    EditorUtils.DrawLabelPair("Date Imported:",
                                              FileInfo.CreationTime.ToString().RemovePathEnd(" ").RemovePathEnd(" "), extStyle, GUILayout.MaxWidth(165));
                    GUIContent content = new GUIContent("    Open File   ", EditorUtils.FetchIcon("d_Import"));
                    if (GUILayout.Button(content, UIStyles.TextureButton, GUILayout.MinWidth(120), GUILayout.Height(20))) {
                        AssetDatabase.OpenAsset(Model);
                    }
                }

                EditorUtils.DrawSeparatorLines("Internal File Info", true);
                using (new EditorGUILayout.HorizontalScope()) {
                    EditorGUIUtility.labelWidth = 90;
                    Model.globalScale = EditorGUILayout.FloatField("Model Scale", Model.globalScale, GUILayout.MaxWidth(120));
                    EditorGUIUtility.labelWidth = 108;
                    Model.useFileScale = EditorGUILayout.Toggle("Use Unity Units", Model.useFileScale, GUILayout.MaxWidth(120));
                    EditorGUIUtility.labelWidth = 100;
                    EditorGUILayout.LabelField("1 mm (File) to 0.001 m (Unity)", GUILayout.MaxWidth(210));
                    EditorGUIUtility.labelWidth = -1;
                }
                EditorGUILayout.Separator();
                using (new EditorGUILayout.HorizontalScope()) {
                    using (new EditorGUILayout.HorizontalScope()) {
                        using (new EditorGUILayout.VerticalScope()) {
                            using (new EditorGUILayout.HorizontalScope()) {
                                bool value = GUILayout.Toggle(Model.importBlendShapes, "", UIStyles.LowerToggle);
                                if (Model.importBlendShapes != value) Undo.RegisterCompleteObjectUndo(Model, UNDO_PROPERTY);
                                Model.importBlendShapes = value;
                                GUILayout.Label("Import BlendShapes", UIStyles.LeftAlignedLabel);
                                GUILayout.FlexibleSpace();
                            }
                            EditorGUILayout.Separator();
                            using (new EditorGUILayout.HorizontalScope()) {
                                bool value = GUILayout.Toggle(Model.importVisibility, "", UIStyles.LowerToggle);
                                if (Model.importVisibility != value) Undo.RegisterCompleteObjectUndo(Model, UNDO_PROPERTY);
                                Model.importVisibility = value;
                                GUILayout.Label("Import Visibility", UIStyles.LeftAlignedLabel);
                                GUILayout.FlexibleSpace();
                            }
                        }
                    } using (new EditorGUILayout.VerticalScope()) {
                        using (new EditorGUILayout.HorizontalScope()) {
                            GUILayout.Label("Mesh Optimization");
                            var value = (MeshOptimizationFlags) EditorGUILayout.EnumPopup(Model.meshOptimizationFlags, GUILayout.MaxWidth(150));
                            if (Model.meshOptimizationFlags != value) Undo.RegisterCompleteObjectUndo(Model, UNDO_PROPERTY);
                            Model.meshOptimizationFlags = value;
                        }
                        EditorGUILayout.Separator();
                        using (new EditorGUILayout.HorizontalScope()) {
                            GUILayout.Label("Import Normals");
                            var value = (ModelImporterNormals) EditorGUILayout.EnumPopup(Model.importNormals, GUILayout.MaxWidth(150));
                            if (Model.importNormals != value) Undo.RegisterCompleteObjectUndo(Model, UNDO_PROPERTY);
                            Model.importNormals = value;
                        }
                    }
                }
                EditorGUILayout.Separator();
                using (new EditorGUILayout.HorizontalScope()) {
                    GUIContent importerContent = new GUIContent(" Open Model Importer", EditorUtils.FetchIcon("Settings"));
                    if (GUILayout.Button(importerContent, GUILayout.MaxWidth(PANEL_WIDTH / 2), GUILayout.MaxHeight(19))) {
                        EditorUtils.OpenAssetProperties(Model.assetPath);
                    }
                    GUIContent projectContent = new GUIContent(" Show Model In Project", EditorUtils.FetchIcon("d_Folder Icon"));
                    if (GUILayout.Button(projectContent, GUILayout.MaxWidth(PANEL_WIDTH / 2), GUILayout.MaxHeight(19))) {
                        EditorUtils.PingObject(Model);
                    }
                }
                EditorGUILayout.Separator();
                EditorUtils.DrawSeparatorLines("Ext Model Utilities", true);
                using (new EditorGUILayout.HorizontalScope()) {
                    using (new EditorGUILayout.VerticalScope(GUI.skin.box, GUILayout.Width(PANEL_WIDTH * 3f/5f), GUILayout.Height(60))) {
                        if (notes == null) {
                            string defaultText = editNotes ? "" : "<i>No notes were found;</i>";
                            notes = Reader.ModelExtData.notes != null ? string.IsNullOrWhiteSpace(Reader.ModelExtData.notes) 
                                                               ? defaultText : Reader.ModelExtData.notes : defaultText;
                        } using (new EditorGUILayout.HorizontalScope()) {
                            using (new EditorGUILayout.VerticalScope(UIStyles.WindowBox, GUILayout.ExpandHeight(false))) {
                                GUILayout.FlexibleSpace();
                                using (new EditorGUILayout.HorizontalScope()) {
                                    GUILayout.FlexibleSpace();
                                    GUILayout.Label("Notes:");
                                    GUILayout.FlexibleSpace();
                                }
                                GUILayout.FlexibleSpace();
                            }
                            GUIStyle noteStyle = editNotes
                                ? new GUIStyle(GUI.skin.label) { wordWrap = true, richText = true }
                                : new GUIStyle(EditorStyles.boldLabel) { wordWrap = true , richText = true };
                            using (new EditorGUILayout.VerticalScope(editNotes ? new GUIStyle(EditorStyles.textArea) { margin = new RectOffset(0, 0, 0, 2) }
                                                                               : UIStyles.WindowBox)) {
                                using (var noteView = new EditorGUILayout.ScrollViewScope(noteScroll, false, false, GUIStyle.none,
                                       GUI.skin.verticalScrollbar, GUI.skin.scrollView, GUILayout.Height(60))) {
                                    noteScroll = noteView.scrollPosition;
                                    using (new EditorGUILayout.VerticalScope()) {
                                        using (new EditorGUILayout.HorizontalScope()) {
                                            if (!editNotes) {
                                                GUILayout.Label(notes, noteStyle, GUILayout.MinWidth(185));
                                            } else notes = GUILayout.TextArea(notes, noteStyle, GUILayout.MinWidth(185));
                                            EditorGUILayout.Space(15);
                                        }
                                    }     
                                }
                            }
                        } using (new EditorGUILayout.HorizontalScope()) {
                            if (editNotes) {
                                GUI.color = UIColors.Green;
                                if (GUILayout.Button("Save", EditorStyles.miniButton)) {
                                    UpdateAssetNotes(notes);
                                    noteScroll = Vector2.zero;
                                    notes = null;
                                    editNotes = false;
                                }
                                GUI.color = UIColors.Red;
                                if (GUILayout.Button("Cancel", EditorStyles.miniButton)) {
                                    UpdateAssetNotes(Reader.ModelExtData.notes);
                                    noteScroll = Vector2.zero;
                                    notes = null;
                                    editNotes = false;
                                }
                                GUI.color = Color.white;
                            } else if (GUILayout.Button("Edit Notes")) {
                                notes = Reader.ModelExtData.notes;
                                editNotes = true;
                            }
                        }
                    } using (new EditorGUILayout.VerticalScope(GUI.skin.box)) {
                        using (new EditorGUILayout.HorizontalScope(UIStyles.WindowBox)) {
                            GUIStyle labelStyle = new GUIStyle(UIStyles.CenteredLabelBold);
                            labelStyle.fontSize -= 1;
                            GUILayout.Label("Ext Data Status", labelStyle);
                        } using (new EditorGUILayout.HorizontalScope()) {
                            EditorUtils.DrawCustomHelpBox("Version Up-To-Date", EditorUtils.FetchIcon("Valid"), 0, 18);
                        } using (new EditorGUILayout.HorizontalScope()) {
                            EditorUtils.DrawCustomHelpBox("Reimported In Library", EditorUtils.FetchIcon("Valid"), 0, 18);
                        } using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox, GUILayout.Height(18))) {
                            GUI.color = UIColors.Blue;
                            if (GUILayout.Button("<b>Reimport</b>", new GUIStyle(GUI.skin.button) { 
                                                                       fontSize = 11, richText = true }, GUILayout.Height(19))) {
                                ModelAssetLibraryAssetPreprocessorGUI.LibraryReimport(Model);
                            }
                            GUI.color = Color.white;
                        }

                    }
                }
            }
        }
    }
}

public class ModelAssetDatabaseReaderTabMeshes : ModelAssetDatabaseReaderTab {

    private MeshPreview meshPreview;

    /// <summary> Class that bundles properties relevant to the selected mesh for quick handling and disposal; </summary>
    private class SelectedMeshProperties {
        /// <summary> Mesh selected in the Editor Window; </summary>
        public Mesh mesh;
        /// <summary> Gameobject holding the mesh selected in the Editor Window </summary>
        public GameObject gameObject;
        /// <summary> Type of the renderer holding the mesh; </summary>
        public Renderer renderer;
        public Texture2D preview;

        public SelectedMeshProperties(Mesh mesh, GameObject gameObject, Renderer renderer) {
            this.mesh = mesh;
            this.gameObject = gameObject;
            this.renderer = renderer;
        }
    } /// <summary> Relevant properties of the Mesh selected in the GUI; </summary>
    private SelectedMeshProperties SelectedMesh;
    /// <summary> Index of the selected SubMesh in the GUI (+1); </summary>
    private int SelectedSubmeshIndex;

    /// <summary> Vertex count of a single mesh; </summary>
    private int LocalVertexCount;

    /// <summary> Triangle count of a single mesh; </summary>
    private int LocalTriangleCount;

    private static Vector2 meshUpperScroll;
    private static Vector2 meshLowerScroll;

    public ModelAssetDatabaseReaderTabMeshes(ModelAssetDatabaseReader Reader) : base(Reader) { }

    public override void ResetData() {
        SelectedMesh = null;
        SelectedSubmeshIndex = 0;
    }

    private void SetSelectedMesh(Mesh mesh, GameObject gameObject, Renderer renderer) {
        ResetData();
        SelectedMesh = new SelectedMeshProperties(mesh, gameObject, renderer);
        LocalVertexCount = mesh.vertexCount;
        LocalTriangleCount = mesh.triangles.Length;
    }

    private void SetSelectedSubMesh(int index) {
        CleanMeshPreview();
        Reader.CleanObjectPreview();
        if (index > 0) {
            CreateDummyGameObject(SelectedMesh.gameObject);
            Renderer renderer = DummyGameObject.GetComponent<Renderer>();
            Material[] arr = renderer.sharedMaterials;
            arr[index - 1] = Reader.CustomTextures.highlight;
            renderer.sharedMaterials = arr;
        } SelectedSubmeshIndex = index;
    }

    /// <summary>
    /// Draw a mesh preview of the currently selected mesh;
    /// </summary>
    /// <param name="mesh"> Mesh to draw the preview for; </param>
    /// <param name="width"> Width of the Preview's Rect; </param>
    /// <param name="height"> Height of the Preview's Rect; </param>
    private void DrawMeshPreviewEditor(Mesh mesh, float width, float height) {
        Rect rect = GUILayoutUtility.GetRect(width, height);
        if (meshPreview == null) {
            meshPreview = new MeshPreview(mesh);
        } else {
            GUIStyle style = new GUIStyle();
            style.normal.background = Reader.CustomTextures.meshPreviewBackground;
            meshPreview.OnPreviewGUI(rect, style);
        }
    }

    /// <summary>
    /// Dispose of the contents of the current Mesh Preview;
    /// </summary>
    private void CleanMeshPreview() {
        try {
            if (meshPreview != null) {
                meshPreview.Dispose();
                meshPreview = null;
            }
        } catch (System.NullReferenceException) {
            Debug.LogWarning("Nice Assembly Reload! Please disregard this message...");
        }
    }

    /// <summary> GUI Display for the Meshes Section </summary>
    public override void ShowGUI() {
        using (new EditorGUILayout.HorizontalScope()) {
            /// Mesh Preview + Mesh Details;
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(200), GUILayout.Height(200))) {
                using (new EditorGUILayout.VerticalScope(GUI.skin.box, GUILayout.Width(192), GUILayout.Height(192))) {
                    GUILayout.Label("Mesh Preview", UIStyles.CenteredLabel);
                    if (SelectedMesh != null) {
                        if (SelectedSubmeshIndex == 0) {
                            DrawMeshPreviewEditor(SelectedMesh.mesh, 192, 192);
                            GUIContent settingsContent = new GUIContent(" Preview Settings", EditorUtils.FetchIcon("d_Mesh Icon"));
                            if (GUILayout.Button(settingsContent, GUILayout.MaxHeight(19))) {
                                ModelAssetLibraryExtraMeshPreview
                                    .ShowPreviewSettings(meshPreview,
                                                         GUIUtility.GUIToScreenRect(GUILayoutUtility.GetLastRect()));
                            }
                        } else Reader.DrawObjectPreviewEditor(DummyGameObject, 192, 192);
                        using (new EditorGUILayout.HorizontalScope(GUI.skin.box)) {
                            if (GUILayout.Button("Open In Materials")) Reader.SwitchToMaterials(SelectedMesh.renderer);
                        }
                    } else EditorUtils.DrawTexture(Reader.CustomTextures.noMeshPreview, 192, 192);

                    using (new EditorGUILayout.HorizontalScope()) {
                        GUILayout.FlexibleSpace();
                        using (new EditorGUILayout.VerticalScope(GUI.skin.box, GUILayout.Width(196), GUILayout.Height(60))) {
                            if (SelectedMesh != null) {
                                GUILayout.Label("Mesh Details", UIStyles.CenteredLabel);
                                GUILayout.FlexibleSpace();
                                EditorUtils.DrawLabelPair("Vertex Count:", LocalVertexCount.ToString());
                                EditorUtils.DrawLabelPair("Triangle Count: ", LocalTriangleCount.ToString());
                            } else {
                                EditorUtils.DrawScopeCenteredText("No Mesh Selected");
                            }
                        } GUILayout.FlexibleSpace();
                    }
                }
            }

            using (new EditorGUILayout.VerticalScope(GUILayout.MaxWidth(PANEL_WIDTH))) {
                EditorUtils.DrawSeparatorLines("Renderer Details", true);
                if (SelectedMesh != null) {
                    using (new EditorGUILayout.HorizontalScope()) {
                        EditorUtils.DrawLabelPair("Skinned Mesh:", SelectedMesh.renderer is SkinnedMeshRenderer ? "Yes" : "No");
                        GUILayout.FlexibleSpace();
                        EditorUtils.DrawLabelPair("No. Of Submeshes:", SelectedMesh.mesh.subMeshCount.ToString());
                        GUILayout.FlexibleSpace();
                        EditorUtils.DrawLabelPair("Materials Assigned:", SelectedMesh.renderer.sharedMaterials.Length.ToString());
                    } GUIContent propertiesContent = new GUIContent(" Open Mesh Properties", EditorUtils.FetchIcon("Settings"));
                    if (GUILayout.Button(propertiesContent, GUILayout.MaxHeight(19))) EditorUtils.OpenAssetProperties(SelectedMesh.mesh);
                } else {
                    EditorGUILayout.Separator();
                    GUILayout.Label("No Mesh Selected", UIStyles.CenteredLabelBold);
                    EditorGUILayout.Separator();
                }

                EditorUtils.DrawSeparatorLines("Submeshes", true);
                if (SelectedMesh != null) {
                    using (var view = new EditorGUILayout.ScrollViewScope(meshUpperScroll, true, false,
                                                                      GUI.skin.horizontalScrollbar, GUI.skin.verticalScrollbar,
                                                                      GUI.skin.box, GUILayout.MaxWidth(PANEL_WIDTH), GUILayout.MaxHeight(53))) {
                        meshUpperScroll = view.scrollPosition;
                        using (new EditorGUILayout.HorizontalScope()) {
                            for (int i = 0; i < SelectedMesh.mesh.subMeshCount; i++) DrawSubMeshSelectionButton(i + 1);
                        }
                    }
                } else {
                    EditorGUILayout.Separator();
                    GUILayout.Label("No Mesh Selected", UIStyles.CenteredLabelBold);
                    EditorGUILayout.Separator();
                }

                DrawMeshSearchArea();
            }
        }
    }

    /// <summary>
    /// Displays a horizontal scrollview with all the meshes available in the model to select from;
    /// </summary>
    /// <param name="scaleMultiplier"> Lazy scale multiplier; </param>
    /// <param name="selectMaterialRenderer"> Whether the button is being used in the Materials Section; </param>
    private void DrawMeshSearchArea(float scaleMultiplier = 1f, bool selectMaterialRenderer = false) {

        EditorUtils.DrawSeparatorLines("All Meshes", true);

        using (var view = new EditorGUILayout.ScrollViewScope(meshLowerScroll, true, false,
                                                              GUI.skin.horizontalScrollbar, GUIStyle.none,
                                                              GUI.skin.box, GUILayout.MaxWidth(PANEL_WIDTH), GUILayout.MaxHeight(scaleMultiplier == 1 ? 130 : 110))) {
            meshLowerScroll = view.scrollPosition;
            using (new EditorGUILayout.HorizontalScope(GUILayout.MaxWidth(PANEL_WIDTH), GUILayout.MaxHeight(scaleMultiplier == 1 ? 130 : 110))) {
                foreach (MeshRendererPair mrp in Reader.MeshRenderers) {
                    if (mrp.renderer is SkinnedMeshRenderer) {
                        Reader.DrawMeshSelectionButton((mrp.renderer as SkinnedMeshRenderer).sharedMesh,
                                                mrp.renderer.gameObject, mrp.renderer, scaleMultiplier, selectMaterialRenderer);
                    } else if (mrp.renderer is MeshRenderer) {
                        Reader.DrawMeshSelectionButton(mrp.filter.sharedMesh, mrp.renderer.gameObject, mrp.renderer, scaleMultiplier, selectMaterialRenderer);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Update the selected submesh index and update the preview to reflect it;
    /// </summary>
    /// <param name="index"> Index of the submesh to select; </param>
    private void DrawSubMeshSelectionButton(int index) {
        bool isSelected = index == SelectedSubmeshIndex;
        GUIStyle buttonStyle = isSelected ? EditorStyles.helpBox : GUI.skin.box;
        using (new EditorGUILayout.VerticalScope(buttonStyle, GUILayout.MaxWidth(35), GUILayout.MaxHeight(35))) {
            if (GUILayout.Button(index.ToString(), UIStyles.TextureButton, GUILayout.MaxWidth(35), GUILayout.MaxHeight(35))) {
                if (isSelected) index = 0;
                SetSelectedSubMesh(index);
            }
        }
    }
}

public class ModelAssetDatabaseReaderTabMaterials : ModelAssetDatabaseReaderTab {

    private ModelImporter Model { get { return Reader.Model; } }

    /// <summary> Dictionary mapping the current material slot selection; </summary>
    public Dictionary<string, Material> StaticMaterialSlots { get; private set; }

    /// <summary> Dictionary mapping the original material slot selection; </summary>
    public Dictionary<string, Material> OriginalMaterialSlots { get; private set; }

    /// <summary> Whether the current slot selection differs from the old selection; </summary>
    private bool hasStaticSlotChanges;

    /// <summary> Class that bundles properties relevant to the selected material for quick handling and disposal; </summary>
    private class SelectedMaterialProperties {
        public Material material;
        public GameObject gameObject;
        public Renderer renderer;

        public SelectedMaterialProperties(Material material, GameObject gameObject, Renderer renderer) {
            this.material = material;
            this.gameObject = gameObject;
            this.renderer = renderer;
        }

        public SelectedMaterialProperties(Material material) {
            this.material = material;
            gameObject = null;
            renderer = null;
        }

        public SelectedMaterialProperties(GameObject gameObject, Renderer renderer) {
            material = null;
            this.gameObject = gameObject;
            this.renderer = renderer;
        }
    } /// <summary> Relevant properties of the material selected in the GUI; </summary>
    private SelectedMaterialProperties selectedMaterial;

    /// <summary> An Editor Window displayed when a material asset is selected; </summary>
    private ModelAssetDatabaseMaterialInspector MaterialInspectorWindow;

    /// <summary> An Editor Window displaying useful information about staging changes in the Materials Section; </summary>
    private ModelAssetDatabaseMaterialHelper MaterialHelperWindow;

    /// <summary> String to display on material swap undoes; </summary>
    private const string UNDO_MATERIAL_CHANGE = "Material Swap";

    /// <summary> Potential search modes for the Materials Section; </summary>
    private enum MaterialSearchMode {
        /// <summary> Start from a list of meshes and pick from the materials they containt; </summary>
        Mesh,
        /// <summary> Start from a list of materials and pick from a list of meshes that have them assigned; </summary>
        Material
    } /// <summary> Selected distribution of Available Meshes and Materials in the GUI; </summary>
    private static MaterialSearchMode materialSearchMode;

    private static Vector2 topMaterialScroll;
    private static Vector2 leftMaterialScroll;
    private static Vector2 rightMaterialScroll;

    public ModelAssetDatabaseReaderTabMaterials(ModelAssetDatabaseReader Reader) : base(Reader) {

    }

    public override void LoadData(string path) => LoadInternalMaterialMap();

    public override void ResetData() {
        /// Materials Section Dependencies;
        selectedMaterial = null;
        if (hasStaticSlotChanges) {
            if (ModelAssetLibraryModalMaterialChanges.ConfirmMaterialChanges()) {
                AssignMaterialsPersistently();
            } else {
                ResetSlotChanges();
            } try {
                GUIUtility.ExitGUI();
            } catch (ExitGUIException) {
                /// We good :)
            }
        } materialSearchMode = 0;
        CloseMaterialInspectorWindow();
        CloseMaterialHelperWindow();
    }

    /// <summary>
    /// Assigns copies of the material maps in the importer to the static maps in the reader;
    /// </summary>
    private void LoadInternalMaterialMap() {
        OriginalMaterialSlots = MaterialUtils.LoadInternalMaterialMap(Model);
        StaticMaterialSlots = new Dictionary<string, Material>(OriginalMaterialSlots);
    }

    /// <summary>
    /// Override of the Material Replacement method for simple internal use;
    /// </summary>
    /// <param name="key"> Name of the material binding to change; </param>
    /// <param name="newMaterial"> Material to place in the binding; </param>
    public void ReplacePersistentMaterial(string key, Material newMaterial) {
        MaterialUtils.ReplacePersistentMaterial(key, newMaterial, Model);
        StaticMaterialSlots[key] = newMaterial;
        Reader.ReimportModel();
        UpdateSlotChangedStatus();
    }

    /// <summary>
    /// Reverts the serialized references back to their original state;
    /// </summary>
    public void ResetSlotChanges() {
        if (Model == null) return;
        foreach (KeyValuePair<string, Material> kvp in OriginalMaterialSlots) {
            MaterialUtils.ReplacePersistentMaterial(kvp.Key, kvp.Value, Model);
        } StaticMaterialSlots = new Dictionary<string, Material>(OriginalMaterialSlots);
        Reader.ReimportModel();
        hasStaticSlotChanges = false;
    }


    /// <summary>
    /// Assigns the current static dictionary as the persistent material dictionary;
    /// </summary>
    public void AssignMaterialsPersistently() {
        OriginalMaterialSlots = new Dictionary<string, Material>(StaticMaterialSlots);
        hasStaticSlotChanges = false;
    }

    /// <summary>
    /// Compares the current material mapping with the original and decides if they are different;
    /// </summary>
    public void UpdateSlotChangedStatus() {
        if (Model.materialImportMode == 0 || Model.materialLocation == 0) {
            hasStaticSlotChanges = false;
            return;
        }

        foreach (KeyValuePair<string, Material> kvp in StaticMaterialSlots) {
            if (OriginalMaterialSlots[kvp.Key] != kvp.Value) {
                hasStaticSlotChanges = true;
                return;
            }
        } hasStaticSlotChanges = false;
    }

    /// <summary>
    /// Set the Material field of the Selected Material;
    /// <br></br> May be called by the Inspector Window to deselect the current material;
    /// </summary>
    /// <param name="material"> Material to showcase and edit; </param>
    private void SetSelectedMaterial(Material material) {
        if (material != null) CloseMaterialInspectorWindow();
        if (selectedMaterial == null) selectedMaterial = new SelectedMaterialProperties(material);
        else selectedMaterial.material = material;
    }

    /// <summary>
    /// Set the GameObject and Renderer fields of the Selected Material;
    /// </summary>
    /// <param name="gameObject"> GameObject showcasing the material; </param>
    /// <param name="renderer"> Renderer holding the showcased mesh; </param>
    private void SetSelectedRenderer(GameObject gameObject, Renderer renderer) {
        Reader.CleanObjectPreview();
        if (selectedMaterial == null) {
            selectedMaterial = new SelectedMaterialProperties(gameObject, renderer);
        } else if (selectedMaterial.renderer != renderer) {
            selectedMaterial.gameObject = gameObject;
            selectedMaterial.renderer = renderer;
        } CreateDummyGameObject(gameObject);
    }

    /// <summary>
    /// Create a Material Editor and show its OnInspectorGUI() layout;
    /// </summary>
    /// <param name="targetMaterial"> Material to show in the Editor; </param>
    private void DrawMaterialInspector(Material targetMaterial) {
        if (MaterialInspectorWindow == null) {
            MaterialInspectorWindow = ModelAssetDatabaseMaterialInspector.ShowWindow(targetMaterial, MaterialInspectorCallback);
        }
    }

    private void MaterialInspectorCallback(bool closeWindow) {
        if (closeWindow) SetSelectedMaterial(null);
        UpdateObjectPreview();
        MainGUI.Repaint();
    }

    /// <summary>
    /// A method wrapping two other methods often called together to update the object preview;
    /// </summary>
    public void UpdateObjectPreview() {
        Reader.CleanObjectPreview();
        if (selectedMaterial != null && selectedMaterial.gameObject is not null) {
            CreateDummyGameObject(selectedMaterial.gameObject);
        }
    }

    /// <summary>
    /// Close the Material Inspector Window;
    /// </summary>
    private void CloseMaterialInspectorWindow() {
        if (MaterialInspectorWindow != null && EditorWindow.HasOpenInstances<ModelAssetDatabaseMaterialInspector>()) {
            MaterialInspectorWindow.Close();
        }
    }

    /// <summary>
    /// Close the Material Helper Window;
    /// </summary>
    private void CloseMaterialHelperWindow() {
        if (MaterialHelperWindow is not null && EditorWindow.HasOpenInstances<ModelAssetDatabaseMaterialHelper>()) {
            MaterialHelperWindow.Close();
        }
    }


        /// <summary> GUI Display for the Materials Section </summary>
    public void ShowMaterialsSection() {

        if (selectedMaterial != null && selectedMaterial.material != null) {
            DrawMaterialInspector(selectedMaterial.material);
        }

        using (new EditorGUILayout.VerticalScope()) {
            using (new EditorGUILayout.VerticalScope(GUI.skin.box, GUILayout.MaxWidth(660))) {
                EditorUtils.DrawSeparatorLine();
                using (var hscope = new EditorGUILayout.HorizontalScope()) {
                    GUIStyle style = new GUIStyle(UIStyles.CenteredLabelBold);
                    style.contentOffset = new Vector2(0, 1);
                    GUILayout.Label("Material Importer Settings:", style);
                    Model.materialImportMode = (ModelImporterMaterialImportMode) EditorGUILayout.EnumPopup(Model.materialImportMode,
                                                                                                           GUILayout.MaxWidth(140), GUILayout.Height(16));
                    switch (Model.materialImportMode) {
                        case ModelImporterMaterialImportMode.None:
                            EditorUtils.DrawCustomHelpBox(" None: Material Slots are strictly Preview Only;", EditorUtils.FetchIcon("Warning"), 320, 18);
                            UpdateSlotChangedStatus();
                            break;
                        case ModelImporterMaterialImportMode.ImportStandard:
                            EditorUtils.DrawCustomHelpBox(" Standard: Material Slots can be set manually;", EditorUtils.FetchIcon("Valid"), 320, 18);
                            break;
                        case ModelImporterMaterialImportMode.ImportViaMaterialDescription:
                            EditorUtils.DrawCustomHelpBox(" Material: Material Slots can be set manually;", EditorUtils.FetchIcon("Valid"), 320, 18);
                            break;
                    }
                } EditorUtils.DrawSeparatorLine();
            }

            using (new EditorGUILayout.HorizontalScope()) {

                using (new EditorGUILayout.VerticalScope(GUILayout.Width(200), GUILayout.Height(200))) {
                    using (new EditorGUILayout.VerticalScope(GUI.skin.box, GUILayout.Width(200), GUILayout.Height(200))) {
                        GUILayout.Label("Material Preview", UIStyles.CenteredLabel);
                        if (selectedMaterial != null && selectedMaterial.renderer != null) {
                            Reader.DrawObjectPreviewEditor(DummyGameObject, 192, 192);
                            if (GUILayout.Button("Update Preview")) {
                                UpdateObjectPreview();
                            }
                        } else EditorUtils.DrawTexture(Reader.CustomTextures.noMaterialPreview, 192, 192);
                    }

                    using (new EditorGUILayout.HorizontalScope(GUI.skin.box)) {
                        GUILayout.Label("Search Mode:", UIStyles.RightAlignedLabel);
                        materialSearchMode = (MaterialSearchMode) EditorGUILayout.EnumPopup(materialSearchMode);
                    }
                    if (selectedMaterial != null && selectedMaterial.renderer != null) {
                        using (new EditorGUILayout.HorizontalScope(GUI.skin.box)) {
                            if (GUILayout.Button("Open In Meshes")) Reader.SwitchToMeshes(selectedMaterial.renderer);
                        }
                    }
                }

                using (new EditorGUILayout.VerticalScope(GUILayout.MaxWidth(PANEL_WIDTH))) {

                    switch (materialSearchMode) {
                        case MaterialSearchMode.Mesh:
                            DrawMeshSearchArea(0.76f, true);
                            using (new EditorGUILayout.HorizontalScope()) {
                                DrawAvailableMaterials(0.76f);
                                DrawMaterialSlots();
                            } break;
                        case MaterialSearchMode.Material:
                            DrawMaterialSearchArea(0.76f);
                            using (new EditorGUILayout.HorizontalScope()) {
                                DrawAvailableMeshes(0.76f);
                                DrawMaterialSlots();
                            } break;
                    }
                }
            }

            if (Model.materialImportMode == 0) GUI.enabled = false;
            using (new EditorGUILayout.VerticalScope(GUI.skin.box, GUILayout.MaxWidth(660))) {
                EditorUtils.DrawSeparatorLine(); 
                using (var hscope = new EditorGUILayout.HorizontalScope()) {
                    GUIStyle style = new GUIStyle(UIStyles.CenteredLabelBold);
                    style.contentOffset = new Vector2(0, 1);
                    GUILayout.Label("Material Location Settings:", style);
                    var potentialLocation = (ModelImporterMaterialLocation) EditorGUILayout.EnumPopup(Model.materialLocation, 
                                                                                                                     GUILayout.MaxWidth(180));
                    if (Model.materialLocation != potentialLocation) {
                        Model.materialLocation = potentialLocation;
                        Model.SaveAndReimport();
                    } switch (Model.materialLocation) {
                        case ModelImporterMaterialLocation.External:
                            EditorUtils.DrawCustomHelpBox(" External: Material Slots are strictly Preview Only;", EditorUtils.FetchIcon("Warning"), 300, 18);
                            UpdateSlotChangedStatus();
                            break;
                        case ModelImporterMaterialLocation.InPrefab:
                            if (hasStaticSlotChanges) {
                                GUI.color = UIColors.Green;
                                if (GUILayout.Button("<b>Assign Materials</b>", UIStyles.SquashedButton, GUILayout.MaxWidth(125))) {
                                    AssignMaterialsPersistently();
                                } GUI.color = UIColors.Red;
                                if (GUILayout.Button("<b>Revert Materials</b>", UIStyles.SquashedButton, GUILayout.MaxWidth(125))) {
                                    Undo.RecordObject(selectedMaterial.renderer, UNDO_MATERIAL_CHANGE);
                                    ResetSlotChanges();
                                } GUI.color = Color.white;
                                GUIContent helperContent = new GUIContent(EditorUtils.FetchIcon("d__Help"));
                                if (GUILayout.Button(helperContent, GUILayout.MaxWidth(25), GUILayout.MaxHeight(18))) {
                                    MaterialHelperWindow = ModelAssetDatabaseMaterialHelper.ShowWindow(this);
                                }
                            } else {
                                GUI.enabled = false;
                                GUILayout.Button("<b>Material Slots Up-to-Date</b>", UIStyles.SquashedButton, GUILayout.MaxWidth(300), GUILayout.MaxHeight(18));
                                GUI.enabled = true;
                            } break;
                    } 
                } EditorUtils.DrawSeparatorLine();
            } if (Model.materialImportMode == 0) GUI.enabled = true;
        }
    }

    /// <summary>
    /// Draws the 'All materials' scrollview at the top;
    /// <br></br> Drawn in Material Search Mode;
    /// </summary>
    /// <param name="scaleMultiplier"> Lazy scale multiplier; </param>
    private void DrawMaterialSearchArea(float scaleMultiplier = 1f) {

        using (new EditorGUILayout.VerticalScope(GUILayout.Width(PANEL_WIDTH), GUILayout.Height(145))) {
            EditorUtils.DrawSeparatorLines("All Materials", true);
            using (var view = new EditorGUILayout.ScrollViewScope(topMaterialScroll, true, false,
                                                          GUI.skin.horizontalScrollbar, GUIStyle.none,
                                                          GUI.skin.box, GUILayout.MaxWidth(PANEL_WIDTH), GUILayout.MaxHeight(110))) {
                topMaterialScroll = view.scrollPosition;
                using (new EditorGUILayout.HorizontalScope(GUILayout.Width(PANEL_WIDTH), GUILayout.Height(110))) {
                    foreach (Material material in Reader.materialDict.Keys) DrawMaterialButton(material, scaleMultiplier);
                }
            }
        }
    }

    /// <summary>
    /// Draws the 'Available Materials' scrollview at the top;
    /// <br></br> Drawn in Mesh Search Mode;
    /// </summary>
    /// <param name="scaleMultiplier"> Lazy scale multiplier; </param>
    private void DrawAvailableMaterials(float scaleMultiplier = 1f) {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(PANEL_WIDTH / 2), GUILayout.Height(145))) {
            EditorUtils.DrawSeparatorLines("Available Materials", true);
            if (selectedMaterial != null && selectedMaterial.renderer != null) {
                using (var view = new EditorGUILayout.ScrollViewScope(leftMaterialScroll, true, false,
                                                      GUI.skin.horizontalScrollbar, GUIStyle.none,
                                                      GUI.skin.box, GUILayout.MaxWidth(PANEL_WIDTH / 2), GUILayout.MaxHeight(110))) {
                    leftMaterialScroll = view.scrollPosition;
                    using (new EditorGUILayout.HorizontalScope(GUILayout.Width(PANEL_WIDTH / 2), GUILayout.Height(110))) {
                        Material[] uniqueMaterials = Reader.GetUniqueMaterials(selectedMaterial.renderer.sharedMaterials);
                        foreach (Material material in uniqueMaterials) {
                            DrawMaterialButton(material, scaleMultiplier);
                        }
                    }
                }
            } else EditorUtils.DrawScopeCenteredText("No Material Selected");
        }
    }

    /// <summary>
    /// Draws the 'Available Meshes' scrollview at the bottom left;
    /// <br></br> Drawn in Material Search Mode;
    /// </summary>
    /// <param name="scaleMultiplier"> Lazy scale multiplier; </param>
    private void DrawAvailableMeshes(float scaleMultiplier = 1f) {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(PANEL_WIDTH / 2), GUILayout.Height(145))) {
            EditorUtils.DrawSeparatorLines("Available Meshes", true);
            if (selectedMaterial != null && selectedMaterial.material != null) {
                using (var view = new EditorGUILayout.ScrollViewScope(leftMaterialScroll, true, false,
                                                                  GUI.skin.horizontalScrollbar, GUIStyle.none,
                                                                  GUI.skin.box, GUILayout.MaxWidth(PANEL_WIDTH / 2), GUILayout.MaxHeight(110))) {
                    leftMaterialScroll = view.scrollPosition;
                    using (new EditorGUILayout.HorizontalScope(GUILayout.Width(PANEL_WIDTH / 2), GUILayout.Height(110))) {
                        foreach (MeshRendererPair mrp in Reader.MaterialDict[selectedMaterial.material]) {
                            if (mrp.renderer is SkinnedMeshRenderer) {
                                DrawMeshSelectionButton((mrp.renderer as SkinnedMeshRenderer).sharedMesh,
                                                        mrp.renderer.gameObject, mrp.renderer, scaleMultiplier, true);
                            } else if (mrp.renderer is MeshRenderer) {
                                DrawMeshSelectionButton(mrp.filter.sharedMesh, mrp.renderer.gameObject, mrp.renderer, scaleMultiplier, true);
                            }
                        }
                    }
                }
            } else EditorUtils.DrawScopeCenteredText("No Material Selected");
        }
    }

    /// <summary>
    /// Draws the 'Material Slots' scrollview at the bottom right;
    /// <br></br> Drawn for all search modes;
    /// </summary>
    private void DrawMaterialSlots() {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(PANEL_WIDTH / 2), GUILayout.Height(145))) {
            EditorUtils.DrawSeparatorLines("Material Slots", true);
            if (selectedMaterial != null && selectedMaterial.renderer != null) {
                using (var view = new EditorGUILayout.ScrollViewScope(rightMaterialScroll, true, false,
                                                                  GUI.skin.horizontalScrollbar, GUIStyle.none,
                                                                  GUI.skin.box, GUILayout.MaxWidth(PANEL_WIDTH / 2), GUILayout.MaxHeight(110))) {
                    rightMaterialScroll = view.scrollPosition;
                    using (new EditorGUILayout.HorizontalScope(GUILayout.MaxWidth(PANEL_WIDTH / 2), GUILayout.MaxHeight(110))) {
                        Dictionary<string, Material> tempDict = new Dictionary<string, Material>(StaticMaterialSlots);
                        foreach (KeyValuePair<string, Material> kvp in tempDict) {
                            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.MaxWidth(50), GUILayout.MaxHeight(35))) {
                                using (new EditorGUILayout.HorizontalScope(GUILayout.MaxWidth(60))) {
                                    GUILayout.FlexibleSpace();
                                    Material material = (Material) EditorGUILayout.ObjectField(kvp.Value, typeof(Material), false, GUILayout.MaxWidth(50));
                                    if (material != kvp.Value) {
                                        ReplacePersistentMaterial(kvp.Key, material);
                                    }
                                } using (new EditorGUILayout.HorizontalScope(GUILayout.MaxWidth(45))) {
                                    GUILayout.FlexibleSpace();
                                    if (kvp.Value != null) EditorUtils.DrawTexture(AssetPreview.GetAssetPreview(kvp.Value), 40, 40);
                                    else EditorUtils.DrawTexture(EditorUtils.FetchIcon("d_AutoLightbakingOff"), 40, 40);
                                } using (new EditorGUILayout.HorizontalScope(EditorStyles.selectionRect, GUILayout.MaxWidth(40), GUILayout.MaxHeight(8))) {
                                    GUIStyle tempStyle = new GUIStyle(EditorStyles.boldLabel);
                                    tempStyle.fontSize = 8;
                                    GUILayout.Label(kvp.Key, tempStyle, GUILayout.MaxHeight(8), GUILayout.MaxWidth(40));
                                    GUILayout.FlexibleSpace();
                                }
                            }
                        }
                    }
                }
            } else EditorUtils.DrawScopeCenteredText("No Mesh Selected");
        }
    }

    /// <summary>
    /// Draws a button for a selectable material;
    /// </summary>
    /// <param name="material"> Material to draw the button for; </param>
    /// <param name="scaleMultiplier"> A lazy scale multiplier; </param>
    private void DrawMaterialButton(Material material, float scaleMultiplier = 1f) {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.MaxWidth(1))) {
            EditorUtils.DrawTexture(AssetPreview.GetAssetPreview(material), 80 * scaleMultiplier, 80 * scaleMultiplier);
            if (selectedMaterial != null && selectedMaterial.material == material) {
                GUILayout.Label("Selected", UIStyles.CenteredLabelBold, GUILayout.MaxWidth(80 * scaleMultiplier), GUILayout.MaxHeight(14 * scaleMultiplier));
            } else if (GUILayout.Button("Open", GUILayout.MaxWidth(80 * scaleMultiplier))) {
                SetSelectedMaterial(material);
            }
        }
    }
}

public class ModelAssetDatabaseReaderTabPrefabs : ModelAssetDatabaseReaderTab {

    private string ModelPath { get { return Reader.Model.assetPath; } }
    private string ModelID { get { return Reader.ModelID; } }

    /// <summary> The prefab name currently written in the naming Text Field; </summary>
    private string prefabName;

    /// <summary> Class containing relevant Prefab Variant information; </summary>
    private class PrefabVariantData {
        public string guid;
        public string name;
        public PrefabVariantData(string guid, string name) {
            this.guid = guid;
            this.name = name;
        }
    } /// <summary> A list containing all relevant prefab info, to avoid unnecessary operations every frame; </summary>
    private List<PrefabVariantData> PrefabVariantInfo;

    /// <summary> Current state of the name validation process; </summary>
    private GeneralUtils.InvalidNameCondition NameCondition;

    /// <summary> Static log of recent prefab registry changes; </summary>
    private Stack<string> PrefabActionLog;

    private static Vector2 prefabLogScroll;
    private static Vector2 prefabListScroll;

    public ModelAssetDatabaseReaderTabPrefabs(ModelAssetDatabaseReader Reader) : base(Reader) { }

    public override void LoadData(string path) {
        PrefabActionLog = new Stack<string>();
        int prefabCount = UpdatePrefabVariantInfo();
        RegisterPrefabLog("Found " + prefabCount + " Prefab Variant(s) in the Asset Library;");
    }

    /// <summary>
    /// Load and process the Prefab Variant Data from the Model Asset Library for future display;
    /// </summary>
    private int UpdatePrefabVariantInfo() {
        PrefabVariantInfo = new List<PrefabVariantData>();
        List<string> prefabIDs = ModelAssetLibrary.ModelDataDict[ModelID].prefabIDList;
        foreach (string prefabID in prefabIDs) {
            string name = ModelAssetLibrary.PrefabDataDict[prefabID].name + ".prefab";
            PrefabVariantInfo.Add(new PrefabVariantData(prefabID, name));
        } DetermineDefaultPrefabName(ModelPath.ToPrefabPath());
        return prefabIDs.Count;
    }

    /// <summary>
    /// Determine the next default prefab name;
    /// </summary>
    /// <param name="basePath"> Path of the prefab asset; </param>
    /// <param name="name"> Updated inside the recursive stack, no input is required; </param>
    /// <param name="annex"> Updated inside the recursive stack, no input is required; </param>
    private void DetermineDefaultPrefabName(string basePath, string name = null, int annex = 0) {
        if (name == null) {
            name = ModelAssetLibrary.ModelDataDict[ModelID].name.Replace(' ', '_');
            if (char.IsLetter(name[0]) && char.IsLower(name[0])) name = name.Substring(0, 1).ToUpper() + name[1..];
        } string annexedName = name + (annex > 0 ? "_" + annex : "");
        if (ModelAssetLibrary.NoAssetAtPath(basePath + "/" + annexedName + ".prefab")) {
            SetDefaultPrefabName(annexedName);
        } else if (annex < 100) { /// Cheap stack overflow error prevention;
            annex++;
            DetermineDefaultPrefabName(basePath, name, annex);
        }
    }

    /// <summary>
    /// Sets the default prefab name and removes hotcontrol (to update text field);
    /// </summary>
    /// <param name="name"> New default name; </param>
    private void SetDefaultPrefabName(string name) {
        this.prefabName = name;
        GUIUtility.keyboardControl = 0;
        GUIUtility.hotControl = 0;
    }

    /// <summary>
    /// Override for convenient internal use;
    /// </summary>
    /// <returns> True if the name is valid, false otherwise; </returns>
    private bool ValidateFilename() {
        NameCondition = GeneralUtils.ValidateFilename(ModelPath.ToPrefabPathWithName(prefabName), prefabName);
        return NameCondition == 0;
    }

    /// <summary>
    /// Register a prefab in the Model Asset Library with a given name;
    /// </summary>
    /// <param name="modelID"> ID of the model for which the prefab will be registered; </param>
    /// <param name="newPrefabName"> File name of the new prefab variant; </param>
    private void RegisterPrefab(string modelID, string newPrefabName) {
        ModelAssetLibrary.RegisterNewPrefab(modelID, newPrefabName);
        NameCondition = GeneralUtils.InvalidNameCondition.Success;
        UpdatePrefabVariantInfo();
    }

    /// <summary>
    /// Writes a temporary log string with a timestamp to the stack;
    /// </summary>
    /// <param name="log"> String to push to the stack; </param>
    private void RegisterPrefabLog(string log) {
        string logTime = System.DateTime.Now.ToLongTimeString().RemovePathEnd(" ") + ": ";
        PrefabActionLog.Push(logTime + " " + log);
    }

    /// <summary> GUI Display for the Prefabs Section </summary>
    public override void ShowGUI() {

        using (new EditorGUILayout.HorizontalScope(GUILayout.MaxWidth(660))) {
            using (new EditorGUILayout.VerticalScope(GUI.skin.box, GUILayout.MaxWidth(330), GUILayout.MaxHeight(140))) {
                EditorUtils.DrawSeparatorLines("Prefab Variant Registry", true);
                using (new EditorGUILayout.HorizontalScope()) {
                    GUILayout.Label("Register New Prefab Variant:");
                    if (GUILayout.Button("Validate & Register")) {
                        if (ValidateFilename()) {
                            RegisterPrefab(ModelID, prefabName);
                            RegisterPrefabLog("Added Prefab Variant: " + prefabName + ".prefab;");
                        }
                    }
                } string impendingName = EditorGUILayout.TextField("Variant Name:", prefabName);
                if (impendingName != prefabName) {
                    if (NameCondition != 0) NameCondition = 0;
                    SetDefaultPrefabName(impendingName);
                } DrawNameConditionBox();
                GUILayout.FlexibleSpace();
                GUIContent folderContent = new GUIContent(" Show Prefabs Folder", EditorUtils.FetchIcon("d_Folder Icon"));
                if (GUILayout.Button(folderContent, EditorStyles.miniButton, GUILayout.MaxHeight(18))) {
                    EditorUtils.PingObject(AssetDatabase.LoadAssetAtPath<Object>(ModelPath.ToPrefabPath()));
                }
            }

            using (new EditorGUILayout.VerticalScope(GUI.skin.box, GUILayout.MaxWidth(330), GUILayout.MaxHeight(140))) {
                EditorUtils.DrawSeparatorLines("Asset Library Logs", true);
                using (var view = new EditorGUILayout.ScrollViewScope(prefabLogScroll, GUI.skin.box)) {
                    prefabLogScroll = view.scrollPosition;
                    foreach (string line in PrefabActionLog) {
                        GUILayout.Label(line);
                    }
                } GUIContent clearContent = new GUIContent(" Clear", EditorUtils.FetchIcon("d_winbtn_win_close@2x"));
                if (GUILayout.Button(clearContent, EditorStyles.miniButton, GUILayout.MaxHeight(18))) PrefabActionLog.Clear();
            }
        } using (new EditorGUILayout.VerticalScope(GUI.skin.box, GUILayout.MaxWidth(660))) {
            EditorUtils.DrawSeparatorLines("Registered Prefab Variants", true);
            using (var view = new EditorGUILayout.ScrollViewScope(prefabListScroll, GUILayout.ExpandHeight(false))) {
                prefabListScroll = view.scrollPosition;
                DrawPrefabCards();
            }
        }
    }

    /// <summary>
    /// Draw a box with useful information about the chosen file name and prefab creation;
    /// </summary>
    private void DrawNameConditionBox() {
        switch (NameCondition) {
            case GeneralUtils.InvalidNameCondition.None:
                EditorGUILayout.HelpBox("Messages concerning the availability of the name written above will be displayed here;", MessageType.Info);
                break;
            case GeneralUtils.InvalidNameCondition.Empty:
                EditorGUILayout.HelpBox("The name of the file cannot be empty;", MessageType.Error);
                break;
            case GeneralUtils.InvalidNameCondition.Overwrite:
                EditorGUILayout.HelpBox("A file with that name already exists in the target directory. Do you wish to overwrite it?", MessageType.Warning);
                using (new EditorGUILayout.HorizontalScope()) {
                    if (GUILayout.Button("Overwrite")) {
                        RegisterPrefab(ModelID, prefabName);
                        RegisterPrefabLog("Replaced Prefab Variant: " + prefabName + ".prefab;");
                    } if (GUILayout.Button("Cancel")) {
                        NameCondition = 0;
                    }
                } break;
            case GeneralUtils.InvalidNameCondition.Symbol:
                EditorGUILayout.HelpBox("The filename can only contain alphanumerical values and/or whitespace characters;", MessageType.Error);
                break;
            case GeneralUtils.InvalidNameCondition.Convention:
                GUIStyle simulateMargins = new GUIStyle(EditorStyles.helpBox) { margin = new RectOffset(18, 0, 0, 0) };
                using (new EditorGUILayout.HorizontalScope(simulateMargins, GUILayout.MaxHeight(30))) {
                    GUIStyle labelStyle = new GUIStyle();
                    labelStyle.normal.textColor = EditorStyles.helpBox.normal.textColor;
                    labelStyle.fontSize = EditorStyles.helpBox.fontSize;
                    GUILayout.Label(new GUIContent(EditorUtils.FetchIcon("console.erroricon.sml@2x")), labelStyle);
                    using (new EditorGUILayout.VerticalScope()) {
                        GUILayout.FlexibleSpace(); GUILayout.FlexibleSpace(); /// Do not judge me. IT LOOKED OFF OK?!
                        GUILayout.Label("This name violates the project's naming convention;", labelStyle);
                        using (new EditorGUILayout.HorizontalScope()) {
                            GUILayout.Label("More information can be found ", labelStyle, GUILayout.ExpandWidth(false));
                            GUIStyle linkStyle = new GUIStyle(labelStyle);
                            linkStyle.normal.textColor = EditorStyles.linkLabel.normal.textColor;
                            if (GUILayout.Button("here", linkStyle, GUILayout.ExpandWidth(false))) {
                                Application.OpenURL("");
                            } GUILayout.Label(";", labelStyle);
                        } GUILayout.FlexibleSpace(); GUILayout.FlexibleSpace();
                    }
                } break;
            case GeneralUtils.InvalidNameCondition.Success:
                GUIContent messageContent = new GUIContent(" Prefab Variant created successfully!", EditorUtils.FetchIcon("d_PreMatCube@2x"));
                EditorGUILayout.HelpBox(messageContent);
                break;
        }
    }

    /// <summary>
    /// Iterate over the prefab variants of the model and display a set of actions for each of them;
    /// </summary>
    private void DrawPrefabCards() {
        if (PrefabVariantInfo != null && PrefabVariantInfo.Count > 0) {
            foreach (PrefabVariantData prefabData in PrefabVariantInfo) {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox)) {
                    GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel) { contentOffset = new Vector2(2, 2) };
                    GUILayout.Label(prefabData.name, labelStyle, GUILayout.MaxWidth(260));
                    if (GUILayout.Button("Open Prefab", GUILayout.MaxWidth(150), GUILayout.MaxHeight(19))) {
                        EditorUtils.OpenAssetProperties(AssetDatabase.GUIDToAssetPath(prefabData.guid));
                    } if (GUILayout.Button("Open Organizer", GUILayout.MaxWidth(150), GUILayout.MaxHeight(19))) {
                        MainGUI.SwitchToOrganizer(prefabData.guid);
                    } GUI.color = UIColors.Red;
                    GUIContent deleteButton = new GUIContent(EditorUtils.FetchIcon("TreeEditor.Trash"));
                    if (GUILayout.Button(deleteButton, GUILayout.MaxWidth(75), GUILayout.MaxHeight(19))) {
                        if (ModelAssetLibraryModalPrefabDeletion.ConfirmPrefabDeletion(prefabData.name)) {
                            ModelAssetLibrary.DeletePrefab(prefabData.guid);
                            RegisterPrefabLog("Deleted Prefab Variant: " + prefabData.name + ";");
                            UpdatePrefabVariantInfo();
                        } GUIUtility.ExitGUI();
                    } GUI.color = Color.white;
                }
            }
        } else {
            GUILayout.Label("No Prefab Variants have been Registered for this Model;", UIStyles.CenteredLabelBold);
            EditorGUILayout.Separator();
        }
    }
}

public class ModelAssetDatabaseReaderTabRig : ModelAssetDatabaseReaderTab {

    public ModelAssetDatabaseReaderTabRig(ModelAssetDatabaseReader Reader) : base(Reader) {

    }

    public override void LoadData(string path) {

    }

    public override void ResetData() {

    }

    public override void ShowGUI() {

    }
}

public class ModelAssetDatabaseReaderTabAnimations : ModelAssetDatabaseReaderTab {

    /// <summary> Internal editor used to embed the Animation Clip Editor from the Model Importer; </summary>
    private Editor AnimationEditor;

    private static Vector2 animationScroll;

    public ModelAssetDatabaseReaderTabAnimations(ModelAssetDatabaseReader Reader) : base(Reader) { }

    public override void ResetData() => CleanAnimationEditor();

    /// <summary>
    /// Fetches a reference to the Animation Editor class;
    /// </summary>
    private void FetchAnimationEditor() {
        /// Fetch a reference to the base Model Importer Editor class;
        var editorType = typeof(Editor).Assembly.GetType("UnityEditor.ModelImporterEditor");
        /// Perform a clean reconstruction of the Model Importer Editor;
        if (AnimationEditor != null) Object.DestroyImmediate(AnimationEditor);
        AnimationEditor = Editor.CreateEditor(Reader.Model, editorType);
    }

    /// <summary>
    /// Cleans the Animation Editor, if it exists;
    /// </summary>
    private void CleanAnimationEditor() {
        if (AnimationEditor != null) {
            DestroyImmediate(AnimationEditor);
        }
    }

    /// <summary> GUI Display for the Animations Section </summary>
    public override void ShowGUI() {
        if (AnimationEditor == null) FetchAnimationEditor();

        int panelWidth = 620;
        using (new EditorGUILayout.HorizontalScope()) {
            using (new EditorGUILayout.VerticalScope(UIStyles.WindowBox, GUILayout.Width(panelWidth / 2))) {
                EditorUtils.DrawWindowBoxLabel("Animation Editor");
                EditorGUILayout.Separator();
                using (var scope = new EditorGUILayout.ScrollViewScope(animationScroll)) {
                    animationScroll = scope.scrollPosition;
                    DrawAnimationEditor();
                }
            } using (new EditorGUILayout.VerticalScope(UIStyles.WindowBox, GUILayout.Width(panelWidth / 2))) {
                EditorUtils.DrawWindowBoxLabel("Animation Preview");
                using (new EditorGUILayout.VerticalScope(GUI.skin.box)) {
                    if (AnimationEditor.HasPreviewGUI()) {
                        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)) {
                            GUILayout.Label("Preview Settings:", new GUIStyle(GUI.skin.label) { contentOffset = new Vector2(0, -1) });
                            AnimationEditor.OnPreviewSettings();
                        } using (new EditorGUILayout.HorizontalScope()) {
                            GUILayout.FlexibleSpace();
                            using (new EditorGUILayout.VerticalScope()) {
                                Rect rect = GUILayoutUtility.GetRect(panelWidth / 2 + 20, 0, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(true));
                                AnimationEditor.OnInteractivePreviewGUI(rect, GUIStyle.none);
                            } GUILayout.FlexibleSpace();
                        }
                    } else EditorUtils.DrawScopeCenteredText("No animation to preview;");
                } MainGUI.SetHighRepaintFrequency(true);
            } 
        }
    }

    /// <summary>
    /// Draws the Animation Clip Editor tab from the internal Model Importer Editor;
    /// </summary>
    private void DrawAnimationEditor() {
        /// Fetch a reference to the parent Asset Importer Editor, which contains the tabs array field;
        var baseType = typeof(Editor).Assembly.GetType("UnityEditor.AssetImporterTabbedEditor");
        /// Fetch a reference to the Model Importer Clip Editor tab class;
        var tabType = typeof(Editor).Assembly.GetType("UnityEditor.ModelImporterClipEditor");
        /// Fetch a reference to the field containing a tab array;
        var tabField = baseType.GetField("m_Tabs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        /// Fetch a referebce to the OnInspectorGUI method fo the tab;
        var tabGUI = tabType.GetMethod("OnInspectorGUI");
        /// Cast the field value to an array of objects;
        object[] tabArray = (object[]) tabField.GetValue(AnimationEditor);
        /// Access the Animation Clip Editor tab, residing in index 2;
        var animationTab = tabArray[2];
        /// Invoke the method on the Animation Clip Editor tab;
        tabGUI.Invoke(animationTab, null);
    }
}