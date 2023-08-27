using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using CJUtils;
using MADUtils;
using static ModelAssetDatabaseGUI;

/// <summary> Component class of the Model Asset Library;
/// <br></br> Organizes Prefab & Model Data in the GUI for DnD and categorization functions; </summary>
public class ModelAssetDatabasePrefabOrganizer : ModelAssetDatabaseTool {

    #region | Tool Core |

    #region | Variables |

    /// <summary> Data relevant to a folder category, namely, name and file contents; </summary>
    private class FolderData {
        /// <summary> Parsed name of this category; </summary>
        public string name;
        /// <summary> ID list of all prefabs filed under this category;
        /// <br></br> Note that prefab categories is based on model category; </summary>
        public List<string> prefabIDs;
        /// <summary> ID list of all models filed under this category; </summary>
        public List<string> modelIDs;

        public FolderData(string name) {
            this.name = name;
            prefabIDs = new List<string>();
            modelIDs = new List<string>();
        }
    } /// <summary> The folder path of the category selected in the GUI; </summary>
    public string SelectedFolder { get; private set; }

    /// <summary> A dictionary of dictionaries mapping a folder name to it's folder dictionary;
    private Dictionary<string, FolderData> folderMap;

    /// <summary> Data used to draw prefab cards; </summary>
    private class PrefabCardData {
        /// <summary> Root gameObject atop the prefab file hierarchy; </summary>
        public GameObject rootObject;
        /// <summary> Asset preview of the prefab; </summary>
        public Texture2D preview;

        public PrefabCardData(GameObject rootObject, Texture2D preview) {
            this.rootObject = rootObject;
            this.preview = preview;
        }
    } /// <summary> Dictionary mapping path keys to the corresponding Prefab Card Data; </summary>
    private Dictionary<string, PrefabCardData> PrefabCardMap;

    /// <summary> Dictionary mapping path keys to the root game object of a Model; </summary>
    private Dictionary<string, GameObject> ModelCardMap;

    /// <summary> Selection group that may be passed to the DragAndDrop class; </summary>
    private List<Object> DragSelectionGroup;
    /// <summary> Whether the mouse hovered over a button in the current frame; </summary>
    private bool MouseOverButton;

    /// <summary> Constraints for the number of prefabs shown; </summary>
    private enum ScopeMode {
        Folder,
        All
    } /// <summary> Scope Mode selected on the Prefab Organizer GUI; </summary> 
    private ScopeMode scopeMode;

    /// <summary> Sorting Modes for the Prefab Organizer; </summary>
    private enum PrefabSortMode {
        /// <summary> The prefabs cards will be drawn in alphabetical order; </summary>
        Name,
        /// <summary> The prefab cards will be grouped by parent model; </summary>
        Model
    } /// <summary> Sorting Mode selected on the Prefab Organizer GUI; </summary>
    private PrefabSortMode SortMode;

    /// <summary> Search String obtained from the Prefab Organizer Search Bar; </summary>
    private string SearchString;

    /// <summary> List mapping the name of each prefab to their IDs; </summary>
    private List<KeyValuePair<string, string>> prefabNameMapList;
    /// <summary> List holding IDs filtered using the current Search String, if any; </summary>
    private List<string> SearchResultList;

    #endregion

    #region | Initialization & Cleanup |

    protected override void InitializeData() {
        if (folderMap == null) BuildFolderMap();
        prefabNameMapList = new List<KeyValuePair<string, string>>();
    }

    public override void ResetData() {
        
    }

    /// <summary>
    /// Unloads all static data contained in the tool;
    /// </summary>
    public override void FlushData() {
        SelectedFolder = null;
        DragSelectionGroup = null;
        SortMode = 0;

        SearchString = null;
        prefabNameMapList = null;
        SearchResultList = null;
    }

    #endregion

    /// <summary>
    /// Creates a map of folders path and the data they contain that's relevant to the tool;
    /// </summary>
    private void BuildFolderMap() {
        Dictionary<string, ModelAssetLibrary.FolderData> extFolderMap = ModelAssetLibrary.FolderMap;
        if (extFolderMap == null) extFolderMap = ModelAssetLibrary.BuildFolderMap(ModelAssetLibrary.RootAssetPath);
        folderMap = new Dictionary<string, FolderData>();
        foreach (KeyValuePair<string, ModelAssetLibrary.FolderData> kvp in extFolderMap) {
            folderMap[kvp.Key] = new FolderData(kvp.Value.name);
            foreach (string modelPath in kvp.Value.models) {
                folderMap[kvp.Key].modelIDs.Add(AssetDatabase.AssetPathToGUID(modelPath));
            }
            foreach (string modelID in folderMap[kvp.Key].modelIDs) {
                folderMap[kvp.Key].prefabIDs.AddRange(ModelAssetLibrary.ModelDataDict[modelID].prefabIDList);
            }
        }
    }

    /// <summary>
    /// Sets the selected folder path;
    /// </summary>
    /// <param name="path"> Folder path to select; </param>
    public override void SetSelectedAsset(string path) {
        DragSelectionGroup = new List<Object>();
        LoadCategoryData(path);
        SelectedFolder = path;
    }

    /// <summary>
    /// Loads all relevant static data;
    /// </summary>
    /// <param name="path"> Category path to load; </param>
    private void LoadCategoryData(string path) {
        foreach (string prefabID in folderMap[path].prefabIDs) {
            if (!PrefabCardMap.ContainsKey(prefabID)) {
                var prefabData = ModelAssetLibrary.PrefabDataDict[prefabID];
                string assetPath = prefabData.path;
                GameObject rootObject = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                Texture2D preview = AssetPreview.GetAssetPreview(rootObject);
                while (preview == null) {
                    preview = AssetPreview.GetAssetPreview(rootObject);
                } PrefabCardMap[prefabID] = new PrefabCardData(rootObject, preview);
            } string name = ModelAssetLibrary.PrefabDataDict[prefabID].name;
            prefabNameMapList.Add(new KeyValuePair<string, string>(name, prefabID));
        } DragSelectionGroup = new List<Object>();
        foreach (string modelID in folderMap[path].modelIDs) {
            string modelPath = ModelAssetLibrary.ModelDataDict[modelID].path;
            ModelCardMap[modelID] = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        }
    }

    /// <summary>
    /// Sets the current prefab sort mode;
    /// <br></br> Honestly, why did I even write this method;
    /// </summary>
    /// <param name="sortMode"> New sort mode; </param>
    private void SetSortMode(PrefabSortMode sortMode) {
        /// Originally I did a thing or two here, but turns out I don't want to anymore;
        /// So I'll leave this method here anyways <3
        SortMode = sortMode;
    }

    /// <summary>
    /// Updates the current Search String to a new value;
    /// <br></br> Discards the previous results, if any;
    /// </summary>
    /// <param name="searchString"> New search string; </param>
    public void SetSearchString(string searchString) {
        SearchResultList = null;
        SearchString = searchString;
    }

    /// <summary>
    /// Process the Search Results upon request;
    /// </summary>
    private void ProcessSearchList() {
        List<KeyValuePair<string, string>> processList = prefabNameMapList.FindAll((kvp) => kvp.Key.Contains(SearchString));
        SearchResultList = new List<string>();
        foreach (KeyValuePair<string, string> kvp in processList) SearchResultList.Add(kvp.Value);
    }

    #endregion

    #region | Tool GUI |

    private static Vector2 modelSortScroll;

    /// <summary>
    /// Draws the toolbar for the Prefab Organizer;
    /// </summary>
    public override void DrawToolbar() {
        GUI.enabled = false;
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbarButton)) {
            if (SelectedFolder != null) GUI.enabled = true;
            GUILayout.Label("Sort By:", new GUIStyle(UIStyles.ToolbarText) { margin = new RectOffset(0, 20, 1, 0) }, GUILayout.Width(110));
        } if (GUILayout.Button("Name", SortMode == PrefabSortMode.Name
                                       ? UIStyles.SelectedToolbar : EditorStyles.toolbarButton, GUILayout.MinWidth(140), GUILayout.ExpandWidth(true))) {
            SetSortMode(PrefabSortMode.Name);
        } if (GUILayout.Button("Model", SortMode == PrefabSortMode.Model
                                        ? UIStyles.SelectedToolbar : EditorStyles.toolbarButton, GUILayout.MinWidth(140), GUILayout.ExpandWidth(true))) {
            SetSortMode(PrefabSortMode.Model);
        } GUILayout.FlexibleSpace();
        string impendingSearch = EditorGUILayout.TextField(SearchString, EditorStyles.toolbarSearchField, GUILayout.MinWidth(140));
        if (SearchString != impendingSearch) SetSearchString(impendingSearch);
        GUI.enabled = true;
    }

    /// <summary>
    /// Entry point to display the Prefab Organizer Data;
    /// </summary>
    public override void ShowGUI() {

        if (SelectedFolder == null) {
            EditorUtils.DrawScopeCenteredText("Prefabs stored in the Selected Folder will be displayed here;");
            return;
        }

        using (new EditorGUILayout.VerticalScope(UIStyles.WindowBox)) {
            GUILayout.Label(SelectedFolder.IsolatePathEnd("\\/"), UIStyles.CenteredLabelBold);
        }

        using (new EditorGUILayout.HorizontalScope()) {
            if (string.IsNullOrWhiteSpace(SearchString)) {
                switch (SortMode) {
                    case PrefabSortMode.Name:
                        DrawPrefabCards(folderMap[SelectedFolder].prefabIDs, 
                                        "There are no prefabs under this folder;");
                        break;
                    case PrefabSortMode.Model:
                        using (var view = new EditorGUILayout.ScrollViewScope(modelSortScroll,
                                                                                GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(true))) {
                            modelSortScroll = view.scrollPosition;
                            using (new EditorGUILayout.HorizontalScope()) {
                                foreach (string modelID in folderMap[SelectedFolder].modelIDs) DrawModelColumn(modelID);
                            }
                        } break;
                }
            } else DrawSearchCards();
        } 
    }

    /// <summary>
    /// Draws prefab cards whose name matches the Search Query in any way;
    /// </summary>
    private void DrawSearchCards() {
        if (SearchResultList == null) ProcessSearchList();
        DrawPrefabCards(SearchResultList, "No matching results were found;");
    }

    /// <summary>
    /// Draws all prefab cards as stipulated by the default sort mode;
    /// </summary>
    private void DrawPrefabCards(List<string> prefabIDs, string noPrefabsMessage) {
        if (prefabIDs.Count == 0) {
            GUILayout.FlexibleSpace();
            using (new EditorGUILayout.VerticalScope()) {
                EditorGUILayout.Separator(); EditorGUILayout.Separator();
                GUILayout.Label(noPrefabsMessage, UIStyles.CenteredLabelBold);
            }
            GUILayout.FlexibleSpace();
        } else {
            bool validCount = prefabIDs.Count >= 3;
            if (validCount) GUILayout.FlexibleSpace();
            using (new EditorGUILayout.VerticalScope()) {
                int amountPerRow = 3;
                for (int i = 0; i < Mathf.CeilToInt((float) prefabIDs.Count / amountPerRow); i++) {
                    using (new EditorGUILayout.HorizontalScope(GUI.skin.box)) {
                        for (int j = i * amountPerRow; j < Mathf.Min((i + 1) * amountPerRow, prefabIDs.Count); j++) {
                            DrawPrefabCard(prefabIDs[j]);
                        }
                    }
                } DeselectionCheck();
            } if (validCount) GUILayout.FlexibleSpace();
        }
    }

    /// <summary>
    /// Draws a Prefab Card containing buttons;
    /// </summary>
    /// <param name="prefabID"> ID of the prefab to draw the card for; </param>
    private void DrawPrefabCard(string prefabID) {
        PrefabCardData data = PrefabCardMap[prefabID];
        bool objectInSelection = DragSelectionGroup.Contains(data.rootObject);
        if (objectInSelection) GUI.color = UIColors.DarkBlue;
        using (new EditorGUILayout.HorizontalScope(UIStyles.WindowBox, GUILayout.MaxWidth(200), GUILayout.Height(60))) {
            GUI.color = Color.white;
            DrawDragAndDropPreview(prefabID, objectInSelection);

            using (new EditorGUILayout.VerticalScope(UIStyles.WindowBox, GUILayout.ExpandHeight(true))) {
                GUILayout.FlexibleSpace();
                EditorUtils.DrawWindowBoxLabel(ModelAssetLibrary.PrefabDataDict[prefabID].name);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
                    if (GUILayout.Button("Open Library", GUILayout.MaxHeight(24))) {
                        MainGUI.SwitchToLibrary(ModelAssetLibrary.PrefabDataDict[prefabID].modelID);
                    } using (new EditorGUILayout.HorizontalScope()) {
                        if (GUILayout.Button(new GUIContent(EditorUtils.FetchIcon("d_PrefabModel On Icon")), GUILayout.MaxWidth(45), GUILayout.MaxHeight(24))) {
                            EditorUtils.PingObject(AssetImporter.GetAtPath(ModelAssetLibrary
                                                                                .ModelDataDict[ModelAssetLibrary
                                                                                .PrefabDataDict[prefabID].modelID].path)); ;
                        } if (GUILayout.Button(new GUIContent(EditorUtils.FetchIcon("d_PrefabVariant On Icon")), GUILayout.MaxWidth(45), GUILayout.MaxHeight(24))) {
                            EditorUtils.PingObject(data.rootObject);
                        }
                    }
                } GUILayout.FlexibleSpace();
            }
        }
    }

    /// <summary>
    /// Creates a Drag & Drop button for a given prefab;
    /// </summary>
    /// <param name="prefabID"> ID of the prefab; </param>
    /// <param name="objectInSelection"> Whether the prefab is in the current Drag & Drop Selection Group; </param>
    private void DrawDragAndDropPreview(string prefabID, bool objectInSelection) {
        PrefabCardData data = PrefabCardMap[prefabID];
        using (new EditorGUILayout.VerticalScope(UIStyles.WindowBox)) {
            Rect buttonRect = GUILayoutUtility.GetRect(80, 80, GUILayout.ExpandWidth(false));
            if (buttonRect.Contains(Event.current.mousePosition)) {
                MouseOverButton = true;
                bool mouseDown = Event.current.type == EventType.MouseDown;
                bool mouseDrag = Event.current.type == EventType.MouseDrag;
                bool leftClick = Event.current.button == 0;
                bool rightClick = Event.current.button == 1;
                if (Event.current.shift) {
                    if (objectInSelection) {
                        if (mouseDown || (mouseDrag && rightClick)) DragSelectionGroup.Remove(data.rootObject);
                    } else if (mouseDown || (mouseDrag && leftClick)) DragSelectionGroup.Add(data.rootObject);
                } else if (mouseDown && leftClick) {
                    DragAndDrop.PrepareStartDrag();
                    DragAndDrop.StartDrag("Dragging");
                    DragAndDrop.objectReferences = DragSelectionGroup.Count > 1
                                                   ? DragSelectionGroup.ToArray() : new Object[] { data.rootObject };
                    DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                }
            } GUI.Label(buttonRect, PrefabCardMap[prefabID].preview, GUI.skin.button);
        }
    }

    private void DrawDragAndDropPreviewModel(string modelID) {
        using (new EditorGUILayout.VerticalScope(UIStyles.WindowBox)) {
            Rect buttonRect = GUILayoutUtility.GetRect(50, 50, GUILayout.ExpandWidth(false));
            if (buttonRect.Contains(Event.current.mousePosition)) {
                bool mouseDown = Event.current.type == EventType.MouseDown;
                bool leftClick = Event.current.button == 0;
                if (mouseDown && leftClick) {
                    DragAndDrop.PrepareStartDrag();
                    DragAndDrop.StartDrag("Dragging");
                    DragAndDrop.objectReferences = new Object[] { ModelCardMap[modelID] };
                    DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                }
            } GUI.Label(buttonRect, new GUIContent(EditorUtils.FetchIcon("d_PrefabModel Icon")), GUI.skin.button);
        }
    }

    /// <summary>
    /// Whether a Drag & Drop Selection Group wipe may happen at the end of the frame;
    /// </summary>
    private void DeselectionCheck() {
        if (!MouseOverButton && Event.current.type == EventType.MouseDown && !Event.current.shift
            && Event.current.button == 0 && DragSelectionGroup.Count > 0) DragSelectionGroup.Clear();
        MouseOverButton = false;
    }

    /// <summary>
    /// Draw a List of Prefab Cards under a Model Header as stipulated by the model sort mode;
    /// </summary>
    /// <param name="modelID"> ID of the model owning the column; </param>
    private void DrawModelColumn(string modelID) {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(210))) {
            using (new EditorGUILayout.VerticalScope(UIStyles.WindowBox)) {
                using (new EditorGUILayout.HorizontalScope(UIStyles.WindowBox, GUILayout.Width(210))) {
                    DrawDragAndDropPreviewModel(modelID);
                    using (new EditorGUILayout.VerticalScope()) {
                        EditorUtils.DrawWindowBoxLabel(ModelAssetLibrary.ModelDataDict[modelID].name, GUILayout.Height(14));
                        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox, GUILayout.Height(24))) {
                            GUI.color = UIColors.Blue;
                            if (GUILayout.Button("Reimport", EditorStyles.miniButton)) {
                                ModelImporter model = AssetImporter.GetAtPath(ModelAssetLibrary.ModelDataDict[modelID].path) as ModelImporter;
                                ModelAssetLibraryAssetPreprocessorGUI.LibraryReimport(model);
                            } GUI.color = Color.white;
                        }
                    }
                }
            } GUIStyle boxStyle = new GUIStyle(GUI.skin.box) {
                margin = new RectOffset(), stretchWidth = true, stretchHeight = true };
            using (new EditorGUILayout.VerticalScope(boxStyle)) {
                List<string> modelIDList = ModelAssetLibrary.ModelDataDict[modelID].prefabIDList;
                if (modelIDList.Count == 0) {
                    EditorGUILayout.Separator();
                    using (new EditorGUILayout.HorizontalScope()) {
                        GUILayout.FlexibleSpace();
                        using (new EditorGUILayout.VerticalScope(UIStyles.WindowBox)) {
                            GUILayout.Label("No Prefab Variants", UIStyles.CenteredLabelBold);
                            if (GUILayout.Button("Open Library")) MainGUI.SwitchToLibrary(modelID);
                        } GUILayout.FlexibleSpace();
                    }
                } foreach (string prefabID in modelIDList) {
                    using (new EditorGUILayout.HorizontalScope()) {
                        GUILayout.FlexibleSpace();
                        DrawPrefabCard(prefabID);
                        GUILayout.FlexibleSpace();
                    }
                }
            }
        }
    }

    #endregion
}