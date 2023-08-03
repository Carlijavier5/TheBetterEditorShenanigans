using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using CJUtils;
using static ModelAssetLibraryGUI;

/// <summary> Component class of the Model Asset Library;
/// <br></br> Reads asset data and displays the corresponding properties in the GUI; </summary>
public static class ModelAssetLibraryReader {

    #region | General Reference Variables |

    /// <summary> Reference to the model importer file; </summary>
    private static ModelImporter model;
    /// <summary> GUID of the currently selected model; </summary>
    private static string modelID;
    /// <summary> Reference to the prefab, if any, contained in the model; </summary>
    private static GameObject prefab;
    /// <summary> Reference to the Custom Icons Scriptable Object; </summary>
    private static ModelAssetLibraryAssets customTextures;
    /// <summary> Disposable GameObject instantiated to showcase GUI changes non-invasively; </summary>
    private static GameObject dummyGameObject;

    /// <summary> Found myself using this number a lot. Right side width; </summary>
    private static float panelWidth = 440;

    #endregion

    #region | Editor Variables |

    /// <summary> String to display on property undoes; </summary>
    private const string UNDO_PROPERTY = "Model Importer Property Change";

    /// <summary> String to display on material swap undoes; </summary>
    private const string UNDO_MATERIAL_CHANGE = "Material Swap";

    /// <summary> A disposable Editor class embedded in the Editor Window to show a preview of an instantiable asset; </summary>
    private static Editor objectPreview;

    /// <summary> A disposable Editor class embedded in the Editor Window to show a preview of a mesh asset; </summary>
    private static MeshPreview meshPreview;

    /// <summary> An Editor Window displayed when a material asset is selected; </summary>
    private static ModelAssetLibraryMaterialInspector materialInspectorWindow;

    /// <summary> An Editor Window displaying useful information about staging changes in the Materials Section; </summary>
    private static ModelAssetLibraryMaterialHelper materialHelperWindow;

    #endregion

    #region | Model Section Variables |

    /// <summary> The sum of all the vertices in a composite model; </summary>
    public static int GlobalVertexCount { get; private set; }
    /// <summary> The sum of all the triangles in a composite model; </summary>
    public static int GlobalTriangleCount { get; private set; }
    /// <summary> Directory information on the target file; </summary>
    public static FileInfo FileInfo { get; private set; }

    #endregion

    #region | Mesh Section Variables |

    /// <summary> Struct to store renderers with filters and avoid unnecesary GetComponent() calls; </summary>
    private struct MeshRendererPair {
        public MeshFilter filter;
        public Renderer renderer;
        public MeshRendererPair(MeshFilter filter, Renderer renderer) {
            this.filter = filter;
            this.renderer = renderer;
        }
        public override bool Equals(object obj) {
            if (obj is MeshRendererPair) {
                MeshRendererPair mrp = (MeshRendererPair) obj;
                return mrp.filter == filter && mrp.renderer == renderer;
            } return false;
        }
        public override int GetHashCode() {
            return System.HashCode.Combine(filter, renderer);
        }
    } /// <summary> List of all the mesh renderers and mesh filters contained in the model </summary>
    private static List<MeshRendererPair> meshRenderers;

    /// <summary> Class that bundles properties relevant to the selected mesh for quick handling and disposal; </summary>
    private class SelectedMesh {
        /// <summary> Mesh selected in the Editor Window; </summary>
        public Mesh mesh;
        /// <summary> Gameobject holding the mesh selected in the Editor Window </summary>
        public GameObject gameObject;
        /// <summary> Type of the renderer holding the mesh; </summary>
        public Renderer renderer;

        public SelectedMesh(Mesh mesh, GameObject gameObject, Renderer renderer) {
            this.mesh = mesh;
            this.gameObject = gameObject;
            this.renderer = renderer;
        }
    } /// <summary> Relevant properties of the Mesh selected in the GUI; </summary>
    private static SelectedMesh selectedMesh;
    /// <summary> Index of the selected SubMesh in the GUI (+1); </summary>
    private static int selectedSubmeshIndex;

    /// <summary> Vertex count of a single mesh; </summary>
    private static int localVertexCount;

    /// <summary> Triangle count of a single mesh; </summary>
    private static int localTriangleCount;

    private static Vector2 meshUpperScroll;
    private static Vector2 meshLowerScroll;

    #endregion

    #region | Material Section Variables |

    /// <summary> Dictionary mapping each material to the renderers it is available in; </summary>
    private static Dictionary<Material, List<MeshRendererPair>> materialDict;

    /// <summary> Dictionary mapping the current material slot selection; </summary>
    public static Dictionary<string, Material> StaticMaterialSlots { get; set; }

    /// <summary> Dictionary mapping the original material slot selection; </summary>
    public static Dictionary<string, Material> OriginalMaterialSlots { get; set; }

    /// <summary> Whether the current slot selection differs from the old selection; </summary>
    private static bool hasStaticSlotChanges;

    /// <summary> Potential search modes; </summary>
    private enum MaterialSearchMode {
        /// <summary> Start from a list of meshes and pick from the materials they containt; </summary>
        Mesh,
        /// <summary> Start from a list of materials and pick from a list of meshes that have them assigned; </summary>
        Material
    } /// <summary> Selected distribution of Available Meshes and Materials in the GUI; </summary>
    private static MaterialSearchMode materialSearchMode;

    /// <summary> Class that bundles properties relevant to the selected material for quick handling and disposal; </summary>
    private class SelectedMaterial {
        public Material material;
        public GameObject gameObject;
        public Renderer renderer;

        public SelectedMaterial(Material material, GameObject gameObject, Renderer renderer) {
            this.material = material;
            this.gameObject = gameObject;
            this.renderer = renderer;
        }

        public SelectedMaterial(Material material) {
            this.material = material;
            gameObject = null;
            renderer = null;
        }

        public SelectedMaterial(GameObject gameObject, Renderer renderer) {
            material = null;
            this.gameObject = gameObject;
            this.renderer = renderer;
        }
    }
    /// <summary> Relevant properties of the material selected in the GUI; </summary>
    private static SelectedMaterial selectedMaterial;

    private static Vector2 topMaterialScroll;
    private static Vector2 leftMaterialScroll;
    private static Vector2 rightMaterialScroll;

    #endregion

    #region | Prefab Section Variables |

    /// <summary> The prefab name currently written in the naming Text Field; </summary>
    private static string newPrefabName;

    /// <summary> Class containing relevant Prefab Variant information; </summary>
    private class PrefabVariantData {
        public string guid;
        public string name;
        public PrefabVariantData(string guid, string name) {
            this.guid = guid;
            this.name = name;
        }
    } /// <summary> A list containing all relevant prefab info, to avoid unnecessary operations every frame; </summary>
    private static List<PrefabVariantData> prefabVariantInfo;

    /// <summary> Potential results for the name validation process; </summary>
    private enum InvalidNameCondition {
        None,
        Empty,
        Overwrite,
        Symbol,
        Convention,
        Success
    } /// <summary> Current state of the name validation process; </summary>
    private static InvalidNameCondition nameCondition = 0;

    /// <summary> Static log of recent prefab registry changes; </summary>
    private static Stack<string> prefabActionLog;

    private static Vector2 prefabLogScroll;
    private static Vector2 prefabListScroll;

    #endregion

    #region | Global Methods |

    /// <summary>
    /// Discard any read information;
    /// <br></br> Required to load new information without generating persistent garbage;
    /// </summary>
    public static void FlushAssetData() {

        modelID = null;

        if (customTextures == null) {
            customTextures = ModelAssetLibraryConfigurationGUI.ToolAssets;
        }

        /// Editor & Section Variables;
        ResetSectionDependencies();

        /// Reference Variables;
        model = null;
        prefab = null;

        /// Model Section Variables;
        GlobalVertexCount = 0;
        GlobalTriangleCount = 0;
        FileInfo = null;

        /// Meshes Section Variables;
        meshRenderers = new List<MeshRendererPair>();

        localVertexCount = 0;
        localTriangleCount = 0;

        /// Materials Section Variables;
        materialDict = new Dictionary<Material, List<MeshRendererPair>>();
        StaticMaterialSlots = null;
        OriginalMaterialSlots = null;
        selectedMaterial = null;
        materialSearchMode = 0;

        /// Prefab Section Variables;
        prefabVariantInfo = null;
        newPrefabName = null;
        nameCondition = 0;
        prefabActionLog = new Stack<string>();

        if (dummyGameObject) Object.DestroyImmediate(dummyGameObject);

        Undo.undoRedoPerformed -= UpdateSlotChangedStatus;
    }

    /// <summary>
    /// Resets variables whose contents depend on a specific section;
    /// </summary>
    public static void ResetSectionDependencies() {
        CleanObjectPreview();
        CleanMeshPreview();
        CloseMaterialInspectorWindow();
        CloseMaterialHelperWindow();

        /// Meshes Section Dependencies;
        if (selectedMesh != null) selectedMesh = null;
        if (selectedSubmeshIndex != 0) selectedSubmeshIndex = 0;
        /// Materials Section Dependencies;
        if (selectedMaterial != null) selectedMaterial = null;
        if (materialSearchMode != 0) materialSearchMode = 0;
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
        }
    }

    /// <summary>
    /// Call the appropriate section function based on GUI Selection;
    /// </summary>
    /// <param name="sectionType"> Section selected in the GUI; </param>
    public static void ShowSelectedSection(SectionType sectionType) {

        if (AreReferencesFlushed()) return;

        switch (sectionType) {
            case SectionType.Model:
                ShowModelSection();
                break;
            case SectionType.Meshes:
                ShowMeshesSection();
                break;
            case SectionType.Materials:
                ShowMaterialsSection();
                break;
            case SectionType.Prefabs:
                ShowPrefabsSection();
                break;
            case SectionType.Rig:
                WIP();
                break;
            case SectionType.Animations:
                WIP();
                break;
            case SectionType.Skeleton:
                WIP();
                break;
        }
    }

    /// <summary>
    /// Assign a reference to the Model importer at the designated path and load corresponding references;
    /// </summary>
    /// <param name="path"> Path to the model to read; </param>
    public static void LoadSelectedAsset(string path) {
        model = AssetImporter.GetAtPath(path) as ModelImporter;
        modelID = AssetDatabase.AssetPathToGUID(model.assetPath);
        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        FileInfo = new FileInfo(path);
        UpdateMeshAndMaterialProperties();
        LoadMaterialDictionaries();
        int prefabCount = UpdatePrefabVariantInfo();
        RegisterPrefabLog("Found " + prefabCount + " Prefab Variant(s) in the Asset Library;");
        Undo.undoRedoPerformed += UpdateSlotChangedStatus;
    }

    #endregion

    #region | Model Section |

    /// <summary> GUI Display for the Model Section </summary>
    public static void ShowModelSection() {

        using (new EditorGUILayout.HorizontalScope()) {
            /// Model Preview + Model Details;
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(200), GUILayout.Height(200))) {
                using (new EditorGUILayout.VerticalScope(GUI.skin.box, GUILayout.Width(192), GUILayout.Height(192))) {
                    GUILayout.Label("Model Preview", UIStyles.CenteredLabel);
                    DrawObjectPreviewEditor(prefab, 192, 192);

                    using (new EditorGUILayout.HorizontalScope()) {
                        GUILayout.FlexibleSpace();
                        using (new EditorGUILayout.VerticalScope(GUI.skin.box, GUILayout.Width(196), GUILayout.Height(100))) {
                            GUILayout.Label("Model Details", UIStyles.CenteredLabel);
                            GUILayout.FlexibleSpace();
                            EditorUtils.DrawLabelPair("Vertex Count:", GlobalVertexCount.ToString());
                            EditorUtils.DrawLabelPair("Triangle Count: ", GlobalTriangleCount.ToString());
                            EditorUtils.DrawLabelPair("Mesh Count: ", meshRenderers.Count.ToString());
                            EditorUtils.DrawLabelPair("Rigged: ", model.avatarSetup == 0 ? "No" : "Yes");
                        } GUILayout.FlexibleSpace();
                    }
                }
            }
            /// Model Data;
            using (var view = new EditorGUILayout.VerticalScope(GUILayout.MaxWidth(panelWidth))) {
                
                EditorUtils.DrawSeparatorLines("External File Info", true);
                using (new EditorGUILayout.HorizontalScope()) {
                    GUILayout.Label("File Path:", new GUIStyle(GUI.skin.label) { contentOffset = new Vector2(0, 1) }, GUILayout.MaxWidth(55));
                    GUIStyle pathStyle = new GUIStyle(EditorStyles.textField) { margin = new RectOffset(0, 0, 3, 2) };
                    EditorGUILayout.SelectableLabel(model.assetPath, pathStyle, GUILayout.MaxWidth(260), GUILayout.MaxHeight(19));
                    //EditorUtils.DrawLabelPair("File Path:", model.assetPath, GUILayout.MaxWidth(240));
                    GUIContent content = new GUIContent("  Open Folder", EditorUtils.FetchIcon("Profiler.Open"));
                    if (GUILayout.Button(content, UIStyles.TextureButton, GUILayout.MinWidth(120), GUILayout.Height(20))) {
                        EditorUtility.RevealInFinder(model.assetPath);
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
                        AssetDatabase.OpenAsset(model);
                    }
                }

                EditorUtils.DrawSeparatorLines("Internal File Info", true);
                using (new EditorGUILayout.HorizontalScope()) {
                    EditorGUIUtility.labelWidth = 90;
                    model.globalScale = EditorGUILayout.FloatField("Model Scale", model.globalScale, GUILayout.MaxWidth(120));
                    EditorGUIUtility.labelWidth = 108;
                    model.useFileScale = EditorGUILayout.Toggle("Use Unity Units", model.useFileScale, GUILayout.MaxWidth(120));
                    EditorGUIUtility.labelWidth = 100;
                    EditorGUILayout.LabelField("1 mm (File) to 0.001 m (Unity)", GUILayout.MaxWidth(210));
                    EditorGUIUtility.labelWidth = -1;
                } EditorGUILayout.Separator();
                using (new EditorGUILayout.HorizontalScope()) {
                    using (new EditorGUILayout.HorizontalScope()) {
                        using (new EditorGUILayout.VerticalScope()) {
                            using (new EditorGUILayout.HorizontalScope()) {
                                bool value = GUILayout.Toggle(model.importBlendShapes, "", UIStyles.LowerToggle);
                                if (model.importBlendShapes != value) Undo.RegisterCompleteObjectUndo(model, UNDO_PROPERTY);
                                model.importBlendShapes = value;
                                GUILayout.Label("Import BlendShapes", UIStyles.LeftAlignedLabel);
                                GUILayout.FlexibleSpace();
                            } EditorGUILayout.Separator();
                            using (new EditorGUILayout.HorizontalScope()) {
                                bool value = GUILayout.Toggle(model.importVisibility, "", UIStyles.LowerToggle);
                                if (model.importVisibility != value) Undo.RegisterCompleteObjectUndo(model, UNDO_PROPERTY);
                                model.importVisibility = value;
                                GUILayout.Label("Import Visibility", UIStyles.LeftAlignedLabel);
                                GUILayout.FlexibleSpace();
                            }
                        }
                    } using (new EditorGUILayout.VerticalScope()) {
                        using (new EditorGUILayout.HorizontalScope()) {
                            GUILayout.Label("Mesh Optimization");
                            var value = (MeshOptimizationFlags) EditorGUILayout.EnumPopup(model.meshOptimizationFlags, GUILayout.MaxWidth(150));
                            if (model.meshOptimizationFlags != value) Undo.RegisterCompleteObjectUndo(model, UNDO_PROPERTY);
                            model.meshOptimizationFlags = value;
                        } EditorGUILayout.Separator();
                        using (new EditorGUILayout.HorizontalScope()) {
                            GUILayout.Label("Import Normals");
                            var value = (ModelImporterNormals) EditorGUILayout.EnumPopup(model.importNormals, GUILayout.MaxWidth(150));
                            if (model.importNormals != value) Undo.RegisterCompleteObjectUndo(model, UNDO_PROPERTY);
                            model.importNormals = value;
                        }
                    }
                } EditorGUILayout.Separator();
                using (new EditorGUILayout.HorizontalScope()) {
                    GUIContent importerContent = new GUIContent(" Open Model Importer", EditorUtils.FetchIcon("Settings"));
                    if (GUILayout.Button(importerContent, GUILayout.MaxWidth(panelWidth/2), GUILayout.MaxHeight(19))) {
                        EditorUtils.OpenAssetProperties(model.assetPath);
                    } GUIContent projectContent = new GUIContent(" Show Model In Project", EditorUtils.FetchIcon("d_Folder Icon"));
                    if (GUILayout.Button(projectContent, GUILayout.MaxWidth(panelWidth/2), GUILayout.MaxHeight(19))) {
                        EditorUtils.OpenProjectWindow();
                        EditorGUIUtility.PingObject(model);
                    }
                }
            }
        }
    }

    #endregion

    #region | Meshes Section |

    /// <summary> GUI Display for the Meshes Section </summary>
    public static void ShowMeshesSection() {
        using (new EditorGUILayout.HorizontalScope()) {
            /// Mesh Preview + Mesh Details;
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(200), GUILayout.Height(200))) {
                using (new EditorGUILayout.VerticalScope(GUI.skin.box, GUILayout.Width(192), GUILayout.Height(192))) {
                    GUILayout.Label("Mesh Preview", UIStyles.CenteredLabel);
                    if (selectedMesh != null) {
                        if (selectedSubmeshIndex == 0) {
                            DrawMeshPreviewEditor(selectedMesh.mesh, 192, 192);
                            GUIContent settingsContent = new GUIContent(" Preview Settings", EditorUtils.FetchIcon("d_Mesh Icon"));
                            if (GUILayout.Button(settingsContent, GUILayout.MaxHeight(19))) {
                                ModelAssetLibraryExtraMeshPreview
                                    .ShowPreviewSettings(meshPreview, 
                                                         GUIUtility.GUIToScreenRect(GUILayoutUtility.GetLastRect()));
                            }
                        } else DrawObjectPreviewEditor(dummyGameObject, 192, 192);
                        using (new EditorGUILayout.HorizontalScope(GUI.skin.box)) {
                            if (GUILayout.Button("Open In Materials")) SwitchToMaterials(selectedMesh.renderer);
                        }
                    } else EditorUtils.DrawTexture(customTextures.noMeshPreview, 192, 192);

                    using (new EditorGUILayout.HorizontalScope()) {
                        GUILayout.FlexibleSpace();
                        using (new EditorGUILayout.VerticalScope(GUI.skin.box, GUILayout.Width(196), GUILayout.Height(60))) {
                            if (selectedMesh != null) {
                                GUILayout.Label("Mesh Details", UIStyles.CenteredLabel);
                                GUILayout.FlexibleSpace();
                                EditorUtils.DrawLabelPair("Vertex Count:", localVertexCount.ToString());
                                EditorUtils.DrawLabelPair("Triangle Count: ", localTriangleCount.ToString());
                            } else {
                                EditorUtils.DrawScopeCenteredText("No Mesh Selected");
                            }
                        } GUILayout.FlexibleSpace();
                    }
                }
            }

            using (new EditorGUILayout.VerticalScope(GUILayout.MaxWidth(panelWidth))) {
                EditorUtils.DrawSeparatorLines("Renderer Details", true);
                if (selectedMesh != null) {
                    using (new EditorGUILayout.HorizontalScope()) {
                        EditorUtils.DrawLabelPair("Skinned Mesh:", selectedMesh.renderer is SkinnedMeshRenderer ? "Yes" : "No");
                        GUILayout.FlexibleSpace();
                        EditorUtils.DrawLabelPair("No. Of Submeshes:", selectedMesh.mesh.subMeshCount.ToString());
                        GUILayout.FlexibleSpace();
                        EditorUtils.DrawLabelPair("Materials Assigned:", selectedMesh.renderer.sharedMaterials.Length.ToString());
                    } GUIContent propertiesContent = new GUIContent(" Open Mesh Properties", EditorUtils.FetchIcon("Settings"));
                    if (GUILayout.Button(propertiesContent, GUILayout.MaxHeight(19))) EditorUtils.OpenAssetProperties(selectedMesh.mesh);
                } else {
                    EditorGUILayout.Separator();
                    GUILayout.Label("No Mesh Selected", UIStyles.CenteredLabelBold);
                    EditorGUILayout.Separator();
                }

                EditorUtils.DrawSeparatorLines("Submeshes", true);
                if (selectedMesh != null) {
                    using (var view = new EditorGUILayout.ScrollViewScope(meshUpperScroll, true, false,
                                                                      GUI.skin.horizontalScrollbar, GUI.skin.verticalScrollbar,
                                                                      GUI.skin.box, GUILayout.MaxWidth(panelWidth), GUILayout.MaxHeight(53))) {
                        meshUpperScroll = view.scrollPosition;
                        using (new EditorGUILayout.HorizontalScope()) {
                            for (int i = 0; i < selectedMesh.mesh.subMeshCount; i++) DrawSubMeshSelectionButton(i + 1);
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

    private static void DrawMeshSearchArea(float scaleMultiplier = 1f, bool selectMaterialRenderer = false) {

        EditorUtils.DrawSeparatorLines("All Meshes", true);

        using (var view = new EditorGUILayout.ScrollViewScope(meshLowerScroll, true, false,
                                                              GUI.skin.horizontalScrollbar, GUI.skin.verticalScrollbar,
                                                              GUI.skin.box, GUILayout.MaxWidth(panelWidth), GUILayout.MaxHeight(scaleMultiplier == 1 ? 130 : 110))) {
            meshLowerScroll = view.scrollPosition;
            using (new EditorGUILayout.HorizontalScope(GUILayout.MaxWidth(panelWidth), GUILayout.MaxHeight(scaleMultiplier == 1 ? 130 : 110))) {
                foreach (MeshRendererPair mrp in meshRenderers) {
                    if (mrp.renderer is SkinnedMeshRenderer) {
                        DrawMeshSelectionButton((mrp.renderer as SkinnedMeshRenderer).sharedMesh,
                                                mrp.renderer.gameObject, mrp.renderer, scaleMultiplier, selectMaterialRenderer);
                    } else if (mrp.renderer is MeshRenderer) {
                        DrawMeshSelectionButton(mrp.filter.sharedMesh, mrp.renderer.gameObject, mrp.renderer, scaleMultiplier, selectMaterialRenderer);
                    }
                }
            }
        }
    }

    private static void DrawMeshSelectionButton(Mesh mesh, GameObject gameObject, Renderer renderer, float scaleMultiplier, bool selectMaterialRenderer = false) {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.MaxWidth(1))) {
            EditorUtils.DrawTexture(AssetPreview.GetAssetPreview(mesh), 80 * scaleMultiplier, 80 * scaleMultiplier);
            if ((selectedMesh != null && selectedMesh.mesh == mesh)
                || (selectedMaterial != null && selectedMaterial.renderer == renderer)) {
                GUILayout.Label("Selected", UIStyles.CenteredLabelBold, GUILayout.MaxWidth(80 * scaleMultiplier), GUILayout.MaxHeight(19 * scaleMultiplier));
            } else if (GUILayout.Button("Open", GUILayout.MaxWidth(80 * scaleMultiplier))) {
                if (selectMaterialRenderer) {
                    SetSelectedRenderer(gameObject, renderer);
                } else {
                    SetSelectedMesh(mesh, gameObject, renderer);
                }
            }
        }
    }

    private static void SetSelectedMesh(Mesh mesh, GameObject gameObject, Renderer renderer) {
        ResetSectionDependencies();
        selectedMesh = new SelectedMesh(mesh, gameObject, renderer);
        localVertexCount = mesh.vertexCount;
        localTriangleCount = mesh.triangles.Length;
    }

    private static void DrawSubMeshSelectionButton(int index) {
        bool isSelected = index == selectedSubmeshIndex;
        GUIStyle buttonStyle = isSelected ? EditorStyles.helpBox : GUI.skin.box;
        using (new EditorGUILayout.VerticalScope(buttonStyle, GUILayout.MaxWidth(35), GUILayout.MaxHeight(35))) {
            if (GUILayout.Button(index.ToString(), UIStyles.TextureButton, GUILayout.MaxWidth(35), GUILayout.MaxHeight(35))) {
                if (isSelected) index = 0;
                SetSelectedSubMesh(index);
            }
        }
    }

    private static void SetSelectedSubMesh(int index) {
        CleanMeshPreview();
        CleanObjectPreview();
        if (index > 0) {
            CreateDummyGameObject(selectedMesh.gameObject);
            Renderer renderer = dummyGameObject.GetComponent<Renderer>();
            Material[] arr = renderer.sharedMaterials;
            arr[index - 1] = customTextures.highlight;
            renderer.sharedMaterials = arr;
        } selectedSubmeshIndex = index;
    }

    private static void SwitchToMaterials(Renderer renderer) {
        SetSelectedSection(SectionType.Materials);
        SetSelectedRenderer(renderer.gameObject, renderer);
    }

    #endregion

    #region | Materials Section |

    /// <summary> GUI Display for the Materials Section </summary>
    public static void ShowMaterialsSection() {

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
                    model.materialImportMode = (ModelImporterMaterialImportMode) EditorGUILayout.EnumPopup(model.materialImportMode,
                                                                                                           GUILayout.MaxWidth(140), GUILayout.Height(16));
                    switch (model.materialImportMode) {
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
                            DrawObjectPreviewEditor(dummyGameObject, 192, 192);
                            if (GUILayout.Button("Update Preview")) {
                                UpdateObjectPreview();
                            }
                        } else EditorUtils.DrawTexture(customTextures.noMaterialPreview, 192, 192);
                    }

                    using (new EditorGUILayout.HorizontalScope(GUI.skin.box)) {
                        GUILayout.Label("Search Mode:", UIStyles.RightAlignedLabel);
                        materialSearchMode = (MaterialSearchMode) EditorGUILayout.EnumPopup(materialSearchMode);
                    }
                    if (selectedMaterial != null && selectedMaterial.renderer != null) {
                        using (new EditorGUILayout.HorizontalScope(GUI.skin.box)) {
                            if (GUILayout.Button("Open In Meshes")) SwitchToMeshes(selectedMaterial.renderer);
                        }
                    }
                }

                using (new EditorGUILayout.VerticalScope(GUILayout.MaxWidth(panelWidth))) {

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

            if (model.materialImportMode == 0) GUI.enabled = false;
            using (new EditorGUILayout.VerticalScope(GUI.skin.box, GUILayout.MaxWidth(660))) {
                EditorUtils.DrawSeparatorLine(); 
                using (var hscope = new EditorGUILayout.HorizontalScope()) {
                    GUIStyle style = new GUIStyle(UIStyles.CenteredLabelBold);
                    style.contentOffset = new Vector2(0, 1);
                    GUILayout.Label("Material Location Settings:", style);
                    var potentialLocation = (ModelImporterMaterialLocation) EditorGUILayout.EnumPopup(model.materialLocation, 
                                                                                                                     GUILayout.MaxWidth(180));
                    if (model.materialLocation != potentialLocation) {
                        model.materialLocation = potentialLocation;
                        model.SaveAndReimport();
                    } switch (model.materialLocation) {
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
                                    materialHelperWindow = ModelAssetLibraryMaterialHelper.ShowWindow(model);
                                }
                            } else {
                                GUI.enabled = false;
                                GUILayout.Button("<b>Material Slots Up-to-Date</b>", UIStyles.SquashedButton, GUILayout.MaxWidth(300), GUILayout.MaxHeight(18));
                                GUI.enabled = true;
                            } break;
                    } 
                } EditorUtils.DrawSeparatorLine();
            } if (model.materialImportMode == 0) GUI.enabled = true;
        }
    }

    /// <summary>
    /// Draws the 'All materials' scrollview at the top;
    /// <br></br> Drawn in Material Search Mode;
    /// </summary>
    /// <param name="scaleMultiplier"> Lazy scale multiplier; </param>
    private static void DrawMaterialSearchArea(float scaleMultiplier = 1f) {

        using (new EditorGUILayout.VerticalScope(GUILayout.Width(panelWidth), GUILayout.Height(145))) {
            EditorUtils.DrawSeparatorLines("All Materials", true);
            using (var view = new EditorGUILayout.ScrollViewScope(topMaterialScroll, true, false,
                                                          GUI.skin.horizontalScrollbar, GUI.skin.verticalScrollbar,
                                                          GUI.skin.box, GUILayout.MaxWidth(panelWidth), GUILayout.MaxHeight(110))) {
                topMaterialScroll = view.scrollPosition;
                using (new EditorGUILayout.HorizontalScope(GUILayout.Width(panelWidth), GUILayout.Height(110))) {
                    foreach (Material material in materialDict.Keys) DrawMaterialButton(material, scaleMultiplier);
                }
            }
        }
    }

    /// <summary>
    /// Draws the 'Available Materials' scrollview at the top;
    /// <br></br> Drawn in Mesh Search Mode;
    /// </summary>
    /// <param name="scaleMultiplier"> Lazy scale multiplier; </param>
    private static void DrawAvailableMaterials(float scaleMultiplier = 1f) {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(panelWidth / 2), GUILayout.Height(145))) {
            EditorUtils.DrawSeparatorLines("Available Materials", true);
            if (selectedMaterial != null && selectedMaterial.renderer != null) {
                using (var view = new EditorGUILayout.ScrollViewScope(leftMaterialScroll, true, false,
                                                      GUI.skin.horizontalScrollbar, GUI.skin.verticalScrollbar,
                                                      GUI.skin.box, GUILayout.MaxWidth(panelWidth / 2), GUILayout.MaxHeight(110))) {
                    leftMaterialScroll = view.scrollPosition;
                    using (new EditorGUILayout.HorizontalScope(GUILayout.Width(panelWidth / 2), GUILayout.Height(110))) {
                        Material[] uniqueMaterials = GetUniqueMaterials(selectedMaterial.renderer.sharedMaterials);
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
    private static void DrawAvailableMeshes(float scaleMultiplier = 1f) {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(panelWidth / 2), GUILayout.Height(145))) {
            EditorUtils.DrawSeparatorLines("Available Meshes", true);
            if (selectedMaterial != null && selectedMaterial.material != null) {
                using (var view = new EditorGUILayout.ScrollViewScope(leftMaterialScroll, true, false,
                                                                  GUI.skin.horizontalScrollbar, GUI.skin.verticalScrollbar,
                                                                  GUI.skin.box, GUILayout.MaxWidth(panelWidth / 2), GUILayout.MaxHeight(110))) {
                    leftMaterialScroll = view.scrollPosition;
                    using (new EditorGUILayout.HorizontalScope(GUILayout.Width(panelWidth / 2), GUILayout.Height(110))) {
                        foreach (MeshRendererPair mrp in materialDict[selectedMaterial.material]) {
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
    private static void DrawMaterialSlots() {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(panelWidth / 2), GUILayout.Height(145))) {
            EditorUtils.DrawSeparatorLines("Material Slots", true);
            if (selectedMaterial != null && selectedMaterial.renderer != null) {
                using (var view = new EditorGUILayout.ScrollViewScope(rightMaterialScroll, true, false,
                                                                  GUI.skin.horizontalScrollbar, GUI.skin.verticalScrollbar,
                                                                  GUI.skin.box, GUILayout.MaxWidth(panelWidth / 2), GUILayout.MaxHeight(110))) {
                    rightMaterialScroll = view.scrollPosition;
                    using (new EditorGUILayout.HorizontalScope(GUILayout.MaxWidth(panelWidth / 2), GUILayout.MaxHeight(110))) {
                        int index = 1;
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
                                    EditorUtils.DrawTexture(AssetPreview.GetAssetPreview(kvp.Value), 40, 40);
                                } using (new EditorGUILayout.HorizontalScope(EditorStyles.selectionRect, GUILayout.MaxWidth(40), GUILayout.MaxHeight(8))) {
                                    GUIStyle tempStyle = new GUIStyle(UIStyles.CenteredLabelBold);
                                    tempStyle.fontSize = 8;
                                    GUILayout.Label((index).ToString(), tempStyle, GUILayout.MaxHeight(8));
                                    GUILayout.FlexibleSpace();
                                } index++;
                            }
                        }
                    }
                }
            } else EditorUtils.DrawScopeCenteredText("No Mesh Selected");
        }
    }

    /// <summary>
    /// Replaces a serialized material reference with another and updates the corresponding dictionaries;
    /// <br></br> Called by the Material Slot Buttons;
    /// </summary>
    /// <param name="name"> Name of the material binding to change; </param>
    /// <param name="newMaterial"> Material to place in the binding; </param>
    private static void ReplacePersistentMaterial(string name, Material newMaterial) {

        using (SerializedObject serializedObject = new SerializedObject(model)) {
            SerializedProperty extObjects = serializedObject.FindProperty("m_ExternalObjects");
            int size = extObjects.arraySize;

            for (int i = 0; i < size; i++) {
                SerializedProperty extObject = extObjects.GetArrayElementAtIndex(i);
                string key = extObject.FindPropertyRelative("first.name").stringValue;
                if (extObject.FindPropertyRelative("first.name").stringValue == name) {
                    extObject.FindPropertyRelative("second").objectReferenceValue = newMaterial;
                    StaticMaterialSlots[key] = newMaterial;
                    break;
                }
            } serializedObject.ApplyModifiedProperties();
        } model.SaveAndReimport();
        UpdateObjectPreview();
        UpdateMeshAndMaterialProperties();
        UpdateSlotChangedStatus();
    }

    /// <summary>
    /// Reverts the serialized references back to their original state;
    /// </summary>
    private static void ResetSlotChanges() {
        if (model == null) return;
        using (SerializedObject serializedObject = new SerializedObject(model)) {
            SerializedProperty extObjects = serializedObject.FindProperty("m_ExternalObjects");
            int size = extObjects.arraySize;

            for (int i = 0; i < size; i++) {
                SerializedProperty extObject = extObjects.GetArrayElementAtIndex(i);
                string key = extObject.FindPropertyRelative("first.name").stringValue;
                extObject.FindPropertyRelative("second").objectReferenceValue = OriginalMaterialSlots[key];
            } serializedObject.ApplyModifiedPropertiesWithoutUndo();
        } StaticMaterialSlots = new Dictionary<string, Material>(OriginalMaterialSlots);
        model.SaveAndReimport();
        hasStaticSlotChanges = false;
        UpdateObjectPreview();
    }

    /// <summary>
    /// Assigns the current static dictionary as the persistent material dictionary;
    /// </summary>
    private static void AssignMaterialsPersistently() {
        OriginalMaterialSlots = new Dictionary<string, Material>(StaticMaterialSlots);
        hasStaticSlotChanges = false;
    }

    /// <summary>
    /// Compares the current material mapping with the original and decides if they are different;
    /// </summary>
    private static void UpdateSlotChangedStatus() {

        if (model.materialImportMode == 0 || model.materialLocation == 0) {
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
    /// Draws a button for a selectable material;
    /// </summary>
    /// <param name="material"> Material to draw the button for; </param>
    /// <param name="scaleMultiplier"> A lazy scale multiplier; </param>
    private static void DrawMaterialButton(Material material, float scaleMultiplier = 1f) {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.MaxWidth(1))) {
            EditorUtils.DrawTexture(AssetPreview.GetAssetPreview(material), 80 * scaleMultiplier, 80 * scaleMultiplier);
            if (selectedMaterial != null && selectedMaterial.material == material) {
                GUILayout.Label("Selected", UIStyles.CenteredLabelBold, GUILayout.MaxWidth(80 * scaleMultiplier), GUILayout.MaxHeight(19 * scaleMultiplier));
            } else if (GUILayout.Button("Open", GUILayout.MaxWidth(80 * scaleMultiplier))) {
                SetSelectedMaterial(material);
            }
        }
    }

    /// <summary>
    /// Set the Material field of the Selected Material;
    /// <br></br> May be called by the Inspector Window to deselect the current material;
    /// </summary>
    /// <param name="material"> Material to showcase and edit; </param>
    public static void SetSelectedMaterial(Material material) {
        if (material != null) CloseMaterialInspectorWindow();
        if (selectedMaterial == null) selectedMaterial = new SelectedMaterial(material);
        else selectedMaterial.material = material;
    }

    /// <summary>
    /// Set the GameObject and Renderer fields of the Selected Material;
    /// </summary>
    /// <param name="gameObject"> GameObject showcasing the material; </param>
    /// <param name="renderer"> Renderer holding the showcased mesh; </param>
    private static void SetSelectedRenderer(GameObject gameObject, Renderer renderer) {
        CleanObjectPreview();
        if (selectedMaterial == null) {
            selectedMaterial = new SelectedMaterial(gameObject, renderer);
        } else if (selectedMaterial.renderer != renderer) {
            selectedMaterial.gameObject = gameObject;
            selectedMaterial.renderer = renderer;
        } CreateDummyGameObject(gameObject);
    }

    /// <summary>
    /// Open the currently selected Mesh in the Meshes tab;
    /// </summary>
    /// <param name="renderer"> Renderer holding the mesh; </param>
    private static void SwitchToMeshes(Renderer renderer) {
        SetSelectedSection(SectionType.Meshes);
        MeshRendererPair mrp = GetMRP(renderer);
        Mesh mesh = null;
        if (renderer is SkinnedMeshRenderer) {
            mesh = (mrp.renderer as SkinnedMeshRenderer).sharedMesh;
        } else if (renderer is MeshRenderer) {
            mesh = mrp.filter.sharedMesh;
        } selectedMesh = new SelectedMesh(mesh, renderer.gameObject, renderer);
    }

    #endregion

    #region | Prefabs Section |

    /// <summary> GUI Display for the Prefabs Section </summary>
    public static void ShowPrefabsSection() {

        using (new EditorGUILayout.HorizontalScope(GUILayout.MaxWidth(660))) {
            using (new EditorGUILayout.VerticalScope(GUI.skin.box, GUILayout.MaxWidth(330), GUILayout.MaxHeight(140))) {
                EditorUtils.DrawSeparatorLines("Prefab Variant Registry", true);
                using (new EditorGUILayout.HorizontalScope()) {
                    GUILayout.Label("Register New Prefab Variant:");
                    if (GUILayout.Button("Validate & Register")) {
                        if (ValidateFilename()) {
                            RegisterPrefab(modelID, newPrefabName);
                            RegisterPrefabLog("Added Prefab Variant: " + newPrefabName + ".prefab;");
                        }
                    }
                } string impendingName = EditorGUILayout.TextField("Variant Name:", newPrefabName);
                if (impendingName != newPrefabName) {
                    if (nameCondition != 0) nameCondition = 0;
                    newPrefabName = impendingName;
                } DrawNameConditionBox();
                GUILayout.FlexibleSpace();
                GUIContent folderContent = new GUIContent(" Open Prefabs Folder", EditorUtils.FetchIcon("d_Folder Icon"));
                if (GUILayout.Button(folderContent, EditorStyles.miniButton, GUILayout.MaxHeight(18))) {
                    EditorUtils.OpenProjectWindow();
                    EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(model.assetPath.ToPrefabPath()));
                }
            }

            using (new EditorGUILayout.VerticalScope(GUI.skin.box, GUILayout.MaxWidth(330), GUILayout.MaxHeight(140))) {
                EditorUtils.DrawSeparatorLines("Asset Library Logs", true);
                using (var view = new EditorGUILayout.ScrollViewScope(prefabLogScroll, GUI.skin.box)) {
                    prefabLogScroll = view.scrollPosition;
                    foreach (string line in prefabActionLog) {
                        GUILayout.Label(line);
                    }
                } GUIContent clearContent = new GUIContent(" Clear", EditorUtils.FetchIcon("d_winbtn_win_close@2x")); 
                if (GUILayout.Button(clearContent, EditorStyles.miniButton, GUILayout.MaxHeight(18))) prefabActionLog.Clear();
            }
        }
        using (new EditorGUILayout.VerticalScope(GUI.skin.box, GUILayout.MaxWidth(660))) {
            EditorUtils.DrawSeparatorLines("Registered Prefab Variants", true);
            using (var view = new EditorGUILayout.ScrollViewScope(prefabListScroll, GUILayout.ExpandHeight(false))) {
                prefabListScroll = view.scrollPosition;
                DrawPrefabCards();
            }
        }
    }
    
    /// <summary>
    /// Validate the active filename;
    /// </summary>
    /// <returns> True if the name is valid, false otherwise; </returns>
    private static bool ValidateFilename() {
        if (string.IsNullOrWhiteSpace(newPrefabName)) {
            nameCondition = InvalidNameCondition.Empty;
            return false;
        } if (!ModelAssetLibrary.NoAssetAtPath(model.assetPath.ToPrefabPathWithName(newPrefabName))) {
            nameCondition = InvalidNameCondition.Overwrite;
            return false;
        } if (NameViolatesConvention(newPrefabName)) {
            nameCondition = InvalidNameCondition.Convention;
            return false;
        } List<char> invalidChars = new List<char>(Path.GetInvalidFileNameChars());
        foreach (char character in newPrefabName) {
            if (invalidChars.Contains(character)) {
                nameCondition = InvalidNameCondition.Symbol;
                return false;
            }
        } return true;
    }

    private static bool NameViolatesConvention(string fileName) {
        if (string.IsNullOrWhiteSpace(fileName)) return true;
        if (!char.IsUpper(fileName[0])) return true;
        if (fileName.Contains(" ")) return true;
        return false;
    }

    /// <summary>
    /// Draw a box with useful information about the chosen file name and prefab creation;
    /// </summary>
    private static void DrawNameConditionBox() {
        switch (nameCondition) {
            case InvalidNameCondition.None:
                EditorGUILayout.HelpBox("Messages concerning the availability of the name written above will be displayed here;", MessageType.Info);
                break;
            case InvalidNameCondition.Empty:
                EditorGUILayout.HelpBox("The name of the file cannot be empty;", MessageType.Error);
                break;
            case InvalidNameCondition.Overwrite:
                EditorGUILayout.HelpBox("A file with that name already exists in the target directory. Do you wish to overwrite it?", MessageType.Warning);
                using (new EditorGUILayout.HorizontalScope()) {
                    if (GUILayout.Button("Overwrite")) {
                        RegisterPrefab(modelID, newPrefabName);
                        RegisterPrefabLog("Replaced Prefab Variant: " + newPrefabName + ".prefab;");
                    } if (GUILayout.Button("Cancel")) {
                        nameCondition = InvalidNameCondition.None;
                    }
                } break;
            case InvalidNameCondition.Symbol:
                EditorGUILayout.HelpBox("The filename can only contain alphanumerical values and/or whitespace characters;", MessageType.Error);
                break;
            case InvalidNameCondition.Convention:
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
            case InvalidNameCondition.Success:
                GUIContent messageContent = new GUIContent(" Prefab Variant created successfully!", EditorUtils.FetchIcon("d_PreMatCube@2x"));
                EditorGUILayout.HelpBox(messageContent);
                break;
        }
    }

    /// <summary>
    /// Register a prefab in the Model Asset Library with a given name;
    /// </summary>
    /// <param name="modelID"> ID of the model for which the prefab will be registered; </param>
    /// <param name="newPrefabName"> File name of the new prefab variant; </param>
    private static void RegisterPrefab(string modelID, string newPrefabName) {
        ModelAssetLibrary.RegisterNewPrefab(modelID, newPrefabName);
        nameCondition = InvalidNameCondition.Success;
        UpdatePrefabVariantInfo();
    }

    /// <summary>
    /// Writes a temporary log string with a timestamp to the stack;
    /// </summary>
    /// <param name="log"> String to push to the stack; </param>
    private static void RegisterPrefabLog(string log) {
        string logTime = System.DateTime.Now.ToLongTimeString().RemovePathEnd(" ") + ": ";
        prefabActionLog.Push(logTime + " " + log);
    }

    /// <summary>
    /// Iterate over the prefab variants of the model and display a set of actions for each of them;
    /// </summary>
    private static void DrawPrefabCards() {
        if (prefabVariantInfo != null && prefabVariantInfo.Count > 0) {
            foreach (PrefabVariantData prefabData in prefabVariantInfo) {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox)) {
                    GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel) { contentOffset = new Vector2(2, 2) };
                    GUILayout.Label(prefabData.name, labelStyle, GUILayout.MaxWidth(260));
                    if (GUILayout.Button("Open Prefab", GUILayout.MaxWidth(150), GUILayout.MaxHeight(19))) {
                        EditorUtils.OpenAssetProperties(AssetDatabase.GUIDToAssetPath(prefabData.guid));
                    } if (GUILayout.Button("Level Editor", GUILayout.MaxWidth(150), GUILayout.MaxHeight(19))) {

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

    #endregion

    #region | Helpers |

    /// <summary>
    /// Checks if the model reference is available;
    /// </summary>
    /// <returns> True if the model reference is null, false otherwise; </returns>
    private static bool AreReferencesFlushed() {
        if (model == null) {
            EditorUtils.DrawScopeCenteredText("Whoops, Unity threw a tantrum and these references are gone! \nPlease Reload this... um... thing!");
            return true;
        } return false;
    }

    /// <summary>
    /// Reads all 'accesible' mesh and material data from a model;
    /// </summary>
    private static void UpdateMeshAndMaterialProperties() {
        meshRenderers = new List<MeshRendererPair>();
        MeshFilter[] mfs = prefab.GetComponentsInChildren<MeshFilter>();
        SkinnedMeshRenderer[] smrs = prefab.GetComponentsInChildren<SkinnedMeshRenderer>();

        foreach (MeshFilter mf in mfs) {
            MeshRenderer mr = mf.GetComponent<MeshRenderer>();
            meshRenderers.Add(new MeshRendererPair(mf, mr));
            var sharedMesh = mf.sharedMesh;
            GlobalVertexCount += sharedMesh.vertexCount;
            GlobalTriangleCount += sharedMesh.triangles.Length;
            AssignAllMaterialsInRenderer(mr);
        } foreach (SkinnedMeshRenderer smr in smrs) {
            meshRenderers.Add(new MeshRendererPair(null, smr));
            var sharedMesh = smr.sharedMesh;
            GlobalVertexCount += sharedMesh.vertexCount;
            GlobalTriangleCount += sharedMesh.triangles.Length;
            AssignAllMaterialsInRenderer(smr);
        }
    }

    /// <summary>
    /// Add all the unique material assets on a Mesh Renderer to the Material Dictionary;
    /// </summary>
    /// <param name="renderer"> Renderer to get the materials from; </param>
    private static void AssignAllMaterialsInRenderer(Renderer renderer) {
        foreach (Material material in renderer.sharedMaterials) {
            if (material == null) continue;
            var mrp = GetMRP(renderer);
            if (!materialDict.ContainsKey(material)) materialDict[material] = new List<MeshRendererPair>();
            var res = materialDict[material].Find((pair) => pair.renderer.gameObject == mrp.renderer.gameObject);
            if (res.Equals(default(MeshRendererPair))) materialDict[material].Add(mrp);
        }
    }

    /// <summary>
    /// Fetches a bunch of serialized references from the Model Importer;
    /// </summary>
    private static void LoadMaterialDictionaries() {
        OriginalMaterialSlots = new Dictionary<string, Material>();
        using (SerializedObject serializedObject = new SerializedObject(model)) {
            SerializedProperty extObjects = serializedObject.FindProperty("m_ExternalObjects");

            for (int i = 0; i < extObjects.arraySize; i++) {
                SerializedProperty extObject = extObjects.GetArrayElementAtIndex(i);
                string key = extObject.FindPropertyRelative("first.name").stringValue;
                OriginalMaterialSlots[key] = extObject.FindPropertyRelative("second").objectReferenceValue as Material;
            } serializedObject.ApplyModifiedPropertiesWithoutUndo();
        } StaticMaterialSlots = new Dictionary<string, Material>(OriginalMaterialSlots);
    }

    /// <summary>
    /// Create a MeshRendererPair based on the type of renderer passed;
    /// </summary>
    /// <param name="renderer"> Mesh Renderer which must be turned into a Mesh Renderer Pair; </param>
    /// <returns> Adequate MeshRendererPair for the renderer passed; </returns>
    private static MeshRendererPair GetMRP(Renderer renderer) {
        if (renderer is SkinnedMeshRenderer) {
            return new MeshRendererPair(null, renderer);
        } else {
            return new MeshRendererPair(renderer.GetComponent<MeshFilter>(), renderer);
        }
    }

    /// <summary>
    /// Takes an array of materials and removes all duplicates;
    /// </summary>
    /// <param name="materials"> Array to process; </param>
    /// <returns> Array of unique material assets; </returns>
    private static Material[] GetUniqueMaterials(Material[] materials) {
        List<Material> materialList = new List<Material>();
        foreach (Material material in materials) {
            if (!materialList.Contains(material)) materialList.Add(material);
        }
        return materialList.ToArray();
    }

    /// <summary>
    /// Load and process the Prefab Variant Data from the Model Asset Library for future display;
    /// </summary>
    private static int UpdatePrefabVariantInfo() {
        prefabVariantInfo = new List<PrefabVariantData>();
        List<string> prefabIDs = ModelAssetLibrary.ModelDataDict[modelID].prefabIDList;
        foreach (string prefabID in prefabIDs) {
            string path = ModelAssetLibrary.PrefabDataDict[prefabID].path;
            string name = path.IsolatePathEnd("\\/");
            name = name.RemovePathEnd(".") + ".prefab";
            prefabVariantInfo.Add(new PrefabVariantData(prefabID, name));
        } UpdateDefaultPrefabName(model.assetPath.ToPrefabPath());
        return prefabIDs.Count;
    }

    private static void UpdateDefaultPrefabName(string basePath, string name = null, int annex = 0) {
        if (name == null) name = model.assetPath.IsolatePathEnd("\\/").RemovePathEnd(".");
        string annexedName = name + (annex > 0 ? "_" + annex : "");
        if (ModelAssetLibrary.NoAssetAtPath(basePath + "/" + annexedName + ".prefab")) {
            newPrefabName = annexedName;
        } else if (annex < 100) { /// Cheap stack overflow error prevention;
            annex++;
            UpdateDefaultPrefabName(basePath, name, annex);
        }
    }

    /// <summary>
    /// Create an Object Editor and display its OnPreviewGUI() layout;
    /// </summary>
    /// <param name="gameObject"> GameObject to show in the Preview; </param>
    /// <param name="width"> Width of the Preview's Rect; </param>
    /// <param name="height"> Height of the Preview's Rect; </param>
    private static void DrawObjectPreviewEditor(GameObject gameObject, float width, float height) {
        Rect rect = GUILayoutUtility.GetRect(width, height);
        if (objectPreview == null) {
            if (gameObject == null) {
                EditorUtils.DrawTexture(customTextures.noMeshPreview, width, height);
                return;
            } objectPreview = Editor.CreateEditor(gameObject);
        } else {
            objectPreview.DrawPreview(rect);
            if (objectPreview != null && dummyGameObject) Object.DestroyImmediate(dummyGameObject);
        }
    }

    /// <summary>
    /// Draw a mesh preview of the currently selected mesh;
    /// </summary>
    /// <param name="mesh"> Mesh to draw the preview for; </param>
    /// <param name="width"> Width of the Preview's Rect; </param>
    /// <param name="height"> Height of the Preview's Rect; </param>
    private static void DrawMeshPreviewEditor(Mesh mesh, float width, float height) {
        Rect rect = GUILayoutUtility.GetRect(width, height);
        if (meshPreview == null) {
            meshPreview = new MeshPreview(mesh);
        } else {
            GUIStyle style = new GUIStyle();
            style.normal.background = customTextures.meshPreviewBackground;
            meshPreview.OnPreviewGUI(rect, style);
        }
    }

    /// <summary>
    /// Create a Material Editor and show its OnInspectorGUI() layout;
    /// </summary>
    /// <param name="targetMaterial"> Material to show in the Editor; </param>
    private static void DrawMaterialInspector(Material targetMaterial) {
        if (materialInspectorWindow == null) {
            materialInspectorWindow = ModelAssetLibraryMaterialInspector.ShowWindow(targetMaterial);
        }
    }

    /// <summary>
    /// Dispose of the current Object Preview Editor;
    /// <br></br> May be called by the Material Inspector to update the Preview;
    /// </summary>
    private static void CleanObjectPreview() {
        try {
            if (objectPreview != null) {
                Editor.DestroyImmediate(objectPreview);
            }
        } catch (System.NullReferenceException) {
            Debug.LogWarning("Nice Assembly Reload! Please disregard this message...");
        }
    }

    /// <summary>
    /// Dispose of the contents of the current Mesh Preview;
    /// </summary>
    private static void CleanMeshPreview() {
        try {
            if (meshPreview != null) {
                meshPreview.Dispose();
                meshPreview = null;
            }
        } catch (System.NullReferenceException) {
            Debug.LogWarning("Nice Assembly Reload! Please disregard this message...");
        }
    }

    /// <summary>
    /// Close the Material Inspector Window;
    /// </summary>
    private static void CloseMaterialInspectorWindow() {
        if (materialInspectorWindow != null && EditorWindow.HasOpenInstances<ModelAssetLibraryMaterialInspector>()) {
            materialInspectorWindow.Close();
        }
    }

    /// <summary>
    /// Close the Material Helper Window;
    /// </summary>
    private static void CloseMaterialHelperWindow() {
        if (materialHelperWindow != null && EditorWindow.HasOpenInstances<ModelAssetLibraryMaterialHelper>()) {
            materialHelperWindow.CloseWindow();
        }
    }

    /// <summary>
    /// Creates a dummy GameObject to showcase changes without changing the model prefab;
    /// </summary>
    /// <param name="gameObject"> GameObject to reproduce; </param>
    private static void CreateDummyGameObject(GameObject gameObject) {
        if (dummyGameObject) Object.DestroyImmediate(dummyGameObject);
        dummyGameObject = Object.Instantiate(gameObject);
    }

    /// <summary>
    /// A method wrapping two other methods often called together to update the object preview;
    /// </summary>
    private static void UpdateObjectPreview() {
        CleanObjectPreview();
        if (selectedMaterial != null && selectedMaterial.gameObject != null) {
            CreateDummyGameObject(selectedMaterial.gameObject );
        }
    }

    private static void WIP() {
        EditorUtils.DrawScopeCenteredText("This section is not implemented yet.\nYou should yell at Carlos in response to this great offense!");
    }

    #endregion
}