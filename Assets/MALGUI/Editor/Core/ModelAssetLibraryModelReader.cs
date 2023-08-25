using CJUtils;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using ExtData = ModelAssetLibraryExtData;

/// <summary> Component class of the Model Asset Library;
/// <br></br> Reads asset data and displays the corresponding properties in the GUI; </summary>
public class ModelAssetLibraryModelReader {

    #region | General Section Varaibles |

    /// <summary> Component Tools separated by General purpose; </summary>
    public enum SectionType {
        None,
        Model,
        Meshes,
        Materials,
        Prefabs,
        Rig,
        Animations,
        Skeleton
    } /// <summary> Section currently selected in the GUI; </summary>
    private SectionType ActiveSection;

    /// <summary> Potential Model content limitations; </summary>
    private enum AssetMode {
        Model,
        Animation
    } /// <summary> Asset Mode currently selected in the GUI; </summary>
    private AssetMode ActiveAssetMode;

    /// <summary> Model path currently selected in the GUI; </summary>
    private string SelectedModel;

    #endregion

    #region | General Reference Variables |

    /// <summary> Reference to the model importer file; </summary>
    private ModelImporter Model;
    /// <summary> GUID of the currently selected model; </summary>
    private string ModelID;
    /// <summary> Ext Data of the selected mode; </summary>
    private ExtData ModelExtData;
    /// <summary> Reference to the prefab, if any, contained in the model; </summary>
    private GameObject Prefab;
    /// <summary> Reference to the Custom Icons Scriptable Object; </summary>
    private ModelAssetLibraryAssets CustomTextures;

    /// <summary> Disposable GameObject instantiated to showcase GUI changes non-invasively; </summary>
    private GameObject DummyGameObject;

    #endregion

    #region | Editor Variables |

    /// <summary> A disposable Editor class embedded in the Editor Window to show a preview of an instantiable asset; </summary>
    private Editor ReaderObjectPreview;

    /// <summary> A disposable Editor class embedded in the Editor Window to show a preview of a mesh asset; </summary>
    private MeshPreview ReaderMeshPreview;

    /// <summary> An Editor Window displayed when a material asset is selected; </summary>
    private ModelAssetLibraryMaterialInspector MaterialInspectorWindow;

    /// <summary> An Editor Window displaying useful information about staging changes in the Materials Section; </summary>
    private ModelAssetLibraryMaterialHelper MaterialHelperWindow;

    #endregion

    #region | Model Section Variables |

    /// <summary> The sum of all the vertices in a composite model; </summary>
    private int GlobalVertexCount;
    /// <summary> The sum of all the triangles in a composite model; </summary>
    private int GlobalTriangleCount;
    /// <summary> Directory information on the target file; </summary>
    private FileInfo FileInfo;

    #endregion

    #region | Mesh Section Variables |

    /// <summary> Mesh preview dictionary; </summary>
    private Dictionary<Renderer, Texture2D> MeshPreviewDict;

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
    private List<MeshRendererPair> MeshRenderers;

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

    #endregion

    #region | Material Section Variables |

    /// <summary> Dictionary mapping each material to the renderers it is available in; </summary>
    private Dictionary<Material, List<MeshRendererPair>> MaterialDict;

    /// <summary> Dictionary mapping the current material slot selection; </summary>
    private Dictionary<string, Material> StaticMaterialSlots;

    /// <summary> Dictionary mapping the original material slot selection; </summary>
    private Dictionary<string, Material> OriginalMaterialSlots;

    /// <summary> Whether the current slot selection differs from the old selection; </summary>
    private bool HasStaticSlotChanges;

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
    }
    /// <summary> Relevant properties of the material selected in the GUI; </summary>
    private SelectedMaterialProperties SelectedMaterial;

    #endregion

    #region | Prefab Section Variables |

    /// <summary> The prefab name currently written in the naming Text Field; </summary>
    private string name;

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

    /// <summary> Potential results for the name validation process; </summary>
    public enum InvalidNameCondition {
        None,
        Empty,
        Overwrite,
        Symbol,
        Convention,
        Success
    } /// <summary> Current state of the name validation process; </summary>
    private InvalidNameCondition NameCondition;

    /// <summary> Static log of recent prefab registry changes; </summary>
    private Stack<string> PrefabActionLog;

    #endregion

    #region | Animation Variables |

    /// <summary> Internal editor used to embed the Animation Clip Editor from the Model Importer; </summary>
    private Editor AnimationEditor;

    #endregion

    #region | Global Methods |

    /// <summary>
    /// Set the currently selected asset;
    /// </summary>
    /// <param name="path"> Path to the selected asset; </param>
    public void SetSelectedModel(string path) {
        FlushAssetData();
        LoadSelectedAsset(path);
        ActiveAssetMode = AssetMode.Model;
        ActiveSection = SectionType.Model;
    }

    /// <summary>
    /// Change the Toolbar to deal with a different type of Model content;
    /// </summary>
    /// <param name="newAssetMode"> New model type to atune the toolbar to; </param>
    private void SetSelectedAssetMode(AssetMode newAssetMode) {
        switch (newAssetMode) {
            case AssetMode.Model:
                SetSelectedSection(SectionType.Model);
                break;
            case AssetMode.Animation:
                SetSelectedSection(SectionType.Rig);
                break;
        } ActiveAssetMode = newAssetMode;
    }

    /// <summary>
    /// Sets the GUI's selected Reader Section;
    /// </summary>
    /// <param name="sectionType"> Type of the prospective section to show; </param>
    private void SetSelectedSection(SectionType sectionType) {
        if (ActiveSection != sectionType) {
            ActiveSection = sectionType;
            ResetSectionDependencies();
        }
    }

    /// <summary>
    /// Discard any read information;
    /// <br></br> Required to load new information without generating persistent garbage;
    /// </summary>
    public static void FlushAssetData() {

        ModelID = null;
        ModelExtData = null;

        if (CustomTextures == null) {
            CustomTextures = ModelAssetLibraryConfigurationCore.ToolAssets;
        }

        /// Editor & Section Variables;
        CleanMeshPreviewDictionary();
        ResetSectionDependencies();

        /// Reference Variables;
        Model = null;
        Prefab = null;

        /// Model Section Variables;
        GlobalVertexCount = 0;
        GlobalTriangleCount = 0;
        FileInfo = null;

        /// Meshes Section Variables;
        MeshRenderers = new List<MeshRendererPair>();

        LocalVertexCount = 0;
        LocalTriangleCount = 0;

        /// Materials Section Variables;
        MaterialDict = new Dictionary<Material, List<MeshRendererPair>>();
        StaticMaterialSlots = null;
        OriginalMaterialSlots = null;
        SelectedMaterial = null;

        /// Prefab Section Variables;
        PrefabVariantInfo = null;
        name = null;
        NameCondition = 0;
        PrefabActionLog = new Stack<string>();

        if (DummyGameObject) Object.DestroyImmediate(DummyGameObject);

        Undo.undoRedoPerformed -= UpdateSlotChangedStatus;
    }

    /// <summary>
    /// Resets variables whose contents depend on a specific section;
    /// </summary>
    private void ResetSectionDependencies() {
        CleanObjectPreview();
        CleanMeshPreview();
        CleanAnimationEditor();
        CloseMaterialInspectorWindow();
        CloseMaterialHelperWindow();

        /// Meshes Section Dependencies;
        if (SelectedMesh != null) SelectedMesh = null;
        if (SelectedSubmeshIndex != 0) SelectedSubmeshIndex = 0;
        /// Materials Section Dependencies;
        if (SelectedMaterial != null) SelectedMaterial = null;
        if (HasStaticSlotChanges) {
            if (ModelAssetLibraryModalMaterialChanges.ConfirmMaterialChanges()) {
                AssignMaterialsPersistently();
            } else {
                ResetSlotChanges();
            } try {
                GUIUtility.ExitGUI();
            } catch (ExitGUIException) {
                /// We good :)
            }
        } ModelAssetLibraryModelReaderGUI.RefreshSections();
    }

    /// <summary>
    /// Assign a reference to the Model importer at the designated path and load corresponding references;
    /// </summary>
    /// <param name="path"> Path to the model to read; </param>
    private void LoadSelectedAsset(string path) {
        Model = AssetImporter.GetAtPath(path) as ModelImporter;
        ModelID = AssetDatabase.AssetPathToGUID(Model.assetPath);
        ModelExtData = ModelID != null ? ModelAssetLibrary.ModelDataDict[ModelID].extData : null;
        Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        FileInfo = new FileInfo(path);
        UpdateMeshAndMaterialProperties();
        UpdateInternalMaterialMap();
        int prefabCount = UpdatePrefabVariantInfo();
        RegisterPrefabLog("Found " + prefabCount + " Prefab Variant(s) in the Asset Library;");
        Undo.undoRedoPerformed += UpdateSlotChangedStatus;
    }

    #endregion

    #region | Model Helpers |

    /// <summary>
    /// Updates the Model Notes and disables hot control to properly update the Text Area;
    /// </summary>
    /// <param name="notes"> Notes to pass to the ExtData; </param>
    private void UpdateAssetNotes(string notes) {
        using (var so = new SerializedObject(ModelExtData)) {
            SerializedProperty noteProperty = so.FindProperty("notes");
            noteProperty.stringValue = notes;
            so.ApplyModifiedPropertiesWithoutUndo();
        } GUIUtility.keyboardControl = 0;
        GUIUtility.hotControl = 0;
    }

    #endregion

    #region | Mesh Helpers |

    private void SetSelectedMesh(Mesh mesh, GameObject gameObject, Renderer renderer) {
        ResetSectionDependencies();
        SelectedMesh = new SelectedMeshProperties(mesh, gameObject, renderer);
        LocalVertexCount = mesh.vertexCount;
        LocalTriangleCount = mesh.triangles.Length;
    }

    private void SetSelectedSubMesh(int index) {
        CleanMeshPreview();
        CleanObjectPreview();
        if (index > 0) {
            CreateDummyGameObject(SelectedMesh.gameObject);
            Renderer renderer = DummyGameObject.GetComponent<Renderer>();
            Material[] arr = renderer.sharedMaterials;
            arr[index - 1] = CustomTextures.highlight;
            renderer.sharedMaterials = arr;
        } SelectedSubmeshIndex = index;
    }

    #endregion

    #region | Material Helpers |

    /// <summary>
    /// Override of the Material Replacement method for simple internal use;
    /// </summary>
    /// <param name="key"> Name of the material binding to change; </param>
    /// <param name="newMaterial"> Material to place in the binding; </param>
    private void ReplacePersistentMaterial(string key, Material newMaterial) {
        ReplacePersistentMaterial(key, newMaterial, Model);
        StaticMaterialSlots[key] = newMaterial;
        Model.SaveAndReimport();
        UpdateObjectPreview();
        UpdateMeshAndMaterialProperties();
        UpdateSlotChangedStatus();
    }

    /// <summary>
    /// Reverts the serialized references back to their original state;
    /// </summary>
    public static void ResetSlotChanges() {
        if (Model == null) return;
        foreach (KeyValuePair<string, Material> kvp in OriginalMaterialSlots) {
            ReplacePersistentMaterial(kvp.Key, kvp.Value, Model);
        } StaticMaterialSlots = new Dictionary<string, Material>(OriginalMaterialSlots);
        Model.SaveAndReimport();
        UpdateObjectPreview();
        UpdateMeshAndMaterialProperties();
        HasStaticSlotChanges = false;
    }

    /// <summary>
    /// Assigns the current static dictionary as the persistent material dictionary;
    /// </summary>
    public static void AssignMaterialsPersistently() {
        OriginalMaterialSlots = new Dictionary<string, Material>(StaticMaterialSlots);
        HasStaticSlotChanges = false;
    }

    /// <summary>
    /// Compares the current material mapping with the original and decides if they are different;
    /// </summary>
    public static void UpdateSlotChangedStatus() {

        if (Model.materialImportMode == 0 || Model.materialLocation == 0) {
            HasStaticSlotChanges = false;
            return;
        }

        foreach (KeyValuePair<string, Material> kvp in StaticMaterialSlots) {
            if (OriginalMaterialSlots[kvp.Key] != kvp.Value) {
                HasStaticSlotChanges = true;
                return;
            }
        } HasStaticSlotChanges = false;
    }

    /// <summary>
    /// Set the Material field of the Selected Material;
    /// <br></br> May be called by the Inspector Window to deselect the current material;
    /// </summary>
    /// <param name="material"> Material to showcase and edit; </param>
    public static void SetSelectedMaterial(Material material) {
        if (material != null) CloseMaterialInspectorWindow();
        if (SelectedMaterial == null) SelectedMaterial = new SelectedMaterialProperties(material);
        else SelectedMaterial.material = material;
    }

    /// <summary>
    /// Set the GameObject and Renderer fields of the Selected Material;
    /// </summary>
    /// <param name="gameObject"> GameObject showcasing the material; </param>
    /// <param name="renderer"> Renderer holding the showcased mesh; </param>
    public static void SetSelectedRenderer(GameObject gameObject, Renderer renderer) {
        CleanObjectPreview();
        if (SelectedMaterial == null) {
            SelectedMaterial = new SelectedMaterialProperties(gameObject, renderer);
        } else if (SelectedMaterial.renderer != renderer) {
            SelectedMaterial.gameObject = gameObject;
            SelectedMaterial.renderer = renderer;
        } CreateDummyGameObject(gameObject);
    }

    #endregion

    #region | Prefab Helpers |

    /// <summary>
    /// Load and process the Prefab Variant Data from the Model Asset Library for future display;
    /// </summary>
    public static int UpdatePrefabVariantInfo() {
        PrefabVariantInfo = new List<PrefabVariantData>();
        List<string> prefabIDs = ModelAssetLibrary.ModelDataDict[ModelID].prefabIDList;
        foreach (string prefabID in prefabIDs) {
            string name = ModelAssetLibrary.PrefabDataDict[prefabID].name + ".prefab";
            PrefabVariantInfo.Add(new PrefabVariantData(prefabID, name));
        } DetermineDefaultPrefabName(Model.assetPath.ToPrefabPath());
        return prefabIDs.Count;
    }

    /// <summary>
    /// Determine the next default prefab name;
    /// </summary>
    /// <param name="basePath"> Path of the prefab asset; </param>
    /// <param name="name"> Updated inside the recursive stack, no input is required; </param>
    /// <param name="annex"> Updated inside the recursive stack, no input is required; </param>
    private static void DetermineDefaultPrefabName(string basePath, string name = null, int annex = 0) {
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
    public static void SetDefaultPrefabName(string name) {
        ModelAssetLibraryModelReader.name = name;
        GUIUtility.keyboardControl = 0;
        GUIUtility.hotControl = 0;
    }

    /// <summary>
    /// Validate a filename in terms of content, convention, and File I/O availability;
    /// </summary>
    /// <returns> True if the name is valid, false otherwise; </returns>
    public static InvalidNameCondition ValidateFilename(string path, string name) {
        if (string.IsNullOrWhiteSpace(name)) {
            return InvalidNameCondition.Empty;
        } if (!ModelAssetLibrary.NoAssetAtPath(path)) {
            return InvalidNameCondition.Overwrite;
        } if (NameViolatesConvention(name)) {
            return InvalidNameCondition.Convention;
        } List<char> invalidChars = new List<char>(Path.GetInvalidFileNameChars());
        foreach (char character in name) {
            if (invalidChars.Contains(character)) {
                return InvalidNameCondition.Symbol;
            }
        } return InvalidNameCondition.None;
    }

    /// <summary>
    /// Override for convenient internal use;
    /// </summary>
    /// <returns> True if the name is valid, false otherwise; </returns>
    public static bool ValidateFilename() {
        NameCondition = ValidateFilename(Model.assetPath.ToPrefabPathWithName(name), name);
        return NameCondition == 0;
    }

    private static bool NameViolatesConvention(string fileName) {
        if (string.IsNullOrWhiteSpace(fileName)) return true;
        if (!char.IsUpper(fileName[0])) return true;
        if (fileName.Contains(" ")) return true;
        return false;
    }

    /// <summary>
    /// Register a prefab in the Model Asset Library with a given name;
    /// </summary>
    /// <param name="modelID"> ID of the model for which the prefab will be registered; </param>
    /// <param name="newPrefabName"> File name of the new prefab variant; </param>
    public static void RegisterPrefab(string modelID, string newPrefabName) {
        ModelAssetLibrary.RegisterNewPrefab(modelID, newPrefabName);
        NameCondition = InvalidNameCondition.Success;
        UpdatePrefabVariantInfo();
    }

    /// <summary>
    /// Writes a temporary log string with a timestamp to the stack;
    /// </summary>
    /// <param name="log"> String to push to the stack; </param>
    public static void RegisterPrefabLog(string log) {
        string logTime = System.DateTime.Now.ToLongTimeString().RemovePathEnd(" ") + ": ";
        PrefabActionLog.Push(logTime + " " + log);
    }

    #endregion

    #region | Animation Helpers |

    /// <summary>
    /// Fetches a reference to the Animation Editor class;
    /// </summary>
    public static void FetchAnimationEditor() {
        /// Fetch a reference to the base Model Importer Editor class;
        var editorType = typeof(Editor).Assembly.GetType("UnityEditor.ModelImporterEditor");
        /// Perform a clean reconstruction of the Model Importer Editor;
        if (AnimationEditor != null) Object.DestroyImmediate(AnimationEditor);
        AnimationEditor = Editor.CreateEditor(Model, editorType);
    }

    /// <summary>
    /// Cleans the Animation Editor, if it exists;
    /// </summary>
    public static void CleanAnimationEditor() {
        if (AnimationEditor != null) {
            Object.DestroyImmediate(AnimationEditor);
        }
    }

    #endregion

    #region | Loading Helpers |

    /// <summary>
    /// Checks if the model reference is available;
    /// </summary>
    /// <returns> True if the model reference is null, false otherwise; </returns>
    public static bool ReferencesAreFlushed() => Model == null;

    /// <summary>
    /// Reads all 'accesible' mesh and material data from a model;
    /// </summary>
    private static void UpdateMeshAndMaterialProperties() {
        CleanMeshPreviewDictionary();
        MeshPreviewDict = new Dictionary<Renderer, Texture2D>();
        MeshRenderers = new List<MeshRendererPair>();
        MeshFilter[] mfs = Prefab.GetComponentsInChildren<MeshFilter>();
        SkinnedMeshRenderer[] smrs = Prefab.GetComponentsInChildren<SkinnedMeshRenderer>();

        Mesh dummyMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        MeshPreview mp = new MeshPreview(dummyMesh);
        Resources.UnloadAsset(dummyMesh);
        foreach (MeshFilter mf in mfs) {
            MeshRenderer mr = mf.GetComponent<MeshRenderer>();
            MeshRenderers.Add(new MeshRendererPair(mf, mr));
            var sharedMesh = mf.sharedMesh;
            GlobalVertexCount += sharedMesh.vertexCount;
            GlobalTriangleCount += sharedMesh.triangles.Length;
            mp.mesh = sharedMesh;
            Texture2D previewTexture = mp.RenderStaticPreview(200, 200);
            previewTexture.hideFlags = HideFlags.HideAndDontSave;
            MeshPreviewDict[mr] = previewTexture;
            AssignAllMaterialsInRenderer(mr);
        } foreach (SkinnedMeshRenderer smr in smrs) {
            MeshRenderers.Add(new MeshRendererPair(null, smr));
            var sharedMesh = smr.sharedMesh;
            GlobalVertexCount += sharedMesh.vertexCount;
            GlobalTriangleCount += sharedMesh.triangles.Length;
            mp.mesh = sharedMesh;
            Texture2D previewTexture = mp.RenderStaticPreview(200, 200);
            previewTexture.hideFlags = HideFlags.HideAndDontSave;
            MeshPreviewDict[smr] = previewTexture;
            AssignAllMaterialsInRenderer(smr);
        } mp.Dispose();
    }

    /// <summary>
    /// Add all the unique material assets on a Mesh Renderer to the Material Dictionary;
    /// </summary>
    /// <param name="renderer"> Renderer to get the materials from; </param>
    private static void AssignAllMaterialsInRenderer(Renderer renderer) {
        foreach (Material material in renderer.sharedMaterials) {
            if (material == null) continue;
            var mrp = GetMRP(renderer);
            if (!MaterialDict.ContainsKey(material)) MaterialDict[material] = new List<MeshRendererPair>();
            var res = MaterialDict[material].Find((pair) => pair.renderer.gameObject == mrp.renderer.gameObject);
            if (res.Equals(default(MeshRendererPair))) MaterialDict[material].Add(mrp);
        }
    }

    /// <summary>
    /// Fetches a bunch of internal serialized references from the Model Importer;
    /// </summary>
    public static Dictionary<string, Material> LoadInternalMaterialMap(ModelImporter model) {
        Dictionary<string, Material> internalMap = new Dictionary<string, Material>();
        using (SerializedObject serializedObject = new SerializedObject(model)) {
            SerializedProperty materials = serializedObject.FindProperty("m_Materials");
            SerializedProperty extObjects = serializedObject.FindProperty("m_ExternalObjects");

            for (int matIndex = 0; matIndex < materials.arraySize; matIndex++) {
                SerializedProperty materialID = materials.GetArrayElementAtIndex(matIndex);
                string name = materialID.FindPropertyRelative("name").stringValue;
                string type = materialID.FindPropertyRelative("type").stringValue;

                Object materialRef = null;
                for (int externalObjectIndex = 0; externalObjectIndex < extObjects.arraySize; externalObjectIndex++) {
                    SerializedProperty extObject = extObjects.GetArrayElementAtIndex(externalObjectIndex);
                    string extName = extObject.FindPropertyRelative("first.name").stringValue;
                    string extType = extObject.FindPropertyRelative("first.type").stringValue;

                    if (extType == type && extName == name) {
                        materialRef = extObject.FindPropertyRelative("second").objectReferenceValue;
                        break;
                    }
                } internalMap[name] = materialRef as Material;
            }
        } return internalMap;
    }

    /// <summary>
    /// Assigns copies of the material maps in the importer to the static maps in the reader;
    /// </summary>
    private static void UpdateInternalMaterialMap() {
        OriginalMaterialSlots = LoadInternalMaterialMap(Model);
        StaticMaterialSlots = new Dictionary<string, Material>(OriginalMaterialSlots);
    }

    /// <summary>
    /// Create a MeshRendererPair based on the type of renderer passed;
    /// </summary>
    /// <param name="renderer"> Mesh Renderer which must be turned into a Mesh Renderer Pair; </param>
    /// <returns> Adequate MeshRendererPair for the renderer passed; </returns>
    public static MeshRendererPair GetMRP(Renderer renderer) {
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
    public static Material[] GetUniqueMaterials(Material[] materials) {
        List<Material> materialList = new List<Material>();
        foreach (Material material in materials) {
            if (!materialList.Contains(material)) materialList.Add(material);
        } return materialList.ToArray();
    }

    #endregion

    #region | Preview Helpers |

    /// <summary>
    /// Create an Object Editor and display its OnPreviewGUI() layout;
    /// </summary>
    /// <param name="gameObject"> GameObject to show in the Preview; </param>
    /// <param name="width"> Width of the Preview's Rect; </param>
    /// <param name="height"> Height of the Preview's Rect; </param>
    public static void DrawObjectPreviewEditor(GameObject gameObject, float width, float height) {
        Rect rect = GUILayoutUtility.GetRect(width, height, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
        if (ReaderObjectPreview == null) {
            if (gameObject == null) {
                EditorUtils.DrawTexture(CustomTextures.noMeshPreview, width > 0 ? width : 128, height > 0 ? height : 128);
                return;
            } ReaderObjectPreview = Editor.CreateEditor(gameObject);
        } else {
            ReaderObjectPreview.DrawPreview(rect);
        }
    }

    /// <summary>
    /// An internal overload of the Object Preview method that cleans up the Dummy Object when needed;
    /// </summary>
    /// <param name="gameObject"> GameObject to show in the Preview; </param>
    /// <param name="width"> Width of the Preview's Rect; </param>
    /// <param name="height"> Height of the Preview's Rect; </param>
    public static void DrawInternalObjectPreviewEditor(GameObject gameObject, float width, float height) {
        DrawObjectPreviewEditor(gameObject, width, height);
        if (DummyGameObject && ReaderObjectPreview != null) Object.DestroyImmediate(DummyGameObject);
    }

    /// <summary>
    /// Draw a mesh preview of the currently selected mesh;
    /// </summary>
    /// <param name="mesh"> Mesh to draw the preview for; </param>
    /// <param name="width"> Width of the Preview's Rect; </param>
    /// <param name="height"> Height of the Preview's Rect; </param>
    public static void DrawMeshPreviewEditor(Mesh mesh, float width, float height) {
        Rect rect = GUILayoutUtility.GetRect(width, height);
        if (ReaderMeshPreview == null) {
            ReaderMeshPreview = new MeshPreview(mesh);
        } else {
            GUIStyle style = new GUIStyle();
            style.normal.background = CustomTextures.meshPreviewBackground;
            ReaderMeshPreview.OnPreviewGUI(rect, style);
        }
    }

    /// <summary>
    /// Create a Material Editor and show its OnInspectorGUI() layout;
    /// </summary>
    /// <param name="targetMaterial"> Material to show in the Editor; </param>
    public static void DrawMaterialInspector(Material targetMaterial) {
        if (MaterialInspectorWindow == null) {
            MaterialInspectorWindow = ModelAssetLibraryMaterialInspector.ShowWindow(targetMaterial);
        }
    }

    /// <summary>
    /// Clean up all textures stored in the preview dictionary, as well as the dictionary itself;
    /// <br></br> According to Unity's Memory Profiler, this is safe... probably;
    /// </summary>
    private static void CleanMeshPreviewDictionary() {
        if (MeshPreviewDict == null) return;
        foreach (KeyValuePair<Renderer, Texture2D> kvp in MeshPreviewDict) {
            if (kvp.Value != null) Object.DestroyImmediate(kvp.Value);
        } MeshPreviewDict = null;
    }

    /// <summary>
    /// Dispose of the current Object Preview Editor;
    /// <br></br> May be called by the Material Inspector to update the Preview;
    /// </summary>
    public static void CleanObjectPreview() {
        try {
            if (ReaderObjectPreview != null) {
                Editor.DestroyImmediate(ReaderObjectPreview);
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
            if (ReaderMeshPreview != null) {
                ReaderMeshPreview.Dispose();
                ReaderMeshPreview = null;
            }
        } catch (System.NullReferenceException) {
            Debug.LogWarning("Nice Assembly Reload! Please disregard this message...");
        }
    }

    /// <summary>
    /// Close the Material Inspector Window;
    /// </summary>
    private static void CloseMaterialInspectorWindow() {
        if (MaterialInspectorWindow != null && EditorWindow.HasOpenInstances<ModelAssetLibraryMaterialInspector>()) {
            MaterialInspectorWindow.Close();
        }
    }

    /// <summary>
    /// Close the Material Helper Window;
    /// </summary>
    private static void CloseMaterialHelperWindow() {
        if (MaterialHelperWindow != null && EditorWindow.HasOpenInstances<ModelAssetLibraryMaterialHelper>()) {
            MaterialHelperWindow.CloseWindow();
        }
    }

    /// <summary>
    /// Creates a dummy GameObject to showcase changes without changing the model prefab;
    /// </summary>
    /// <param name="gameObject"> GameObject to reproduce; </param>
    private static void CreateDummyGameObject(GameObject gameObject) {
        if (DummyGameObject) Object.DestroyImmediate(DummyGameObject);
        DummyGameObject = Object.Instantiate(gameObject);
    }

    /// <summary>
    /// A method wrapping two other methods often called together to update the object preview;
    /// </summary>
    public static void UpdateObjectPreview() {
        CleanObjectPreview();
        if (SelectedMaterial != null && SelectedMaterial.gameObject != null) {
            CreateDummyGameObject(SelectedMaterial.gameObject);
        }
    }

    #endregion
}