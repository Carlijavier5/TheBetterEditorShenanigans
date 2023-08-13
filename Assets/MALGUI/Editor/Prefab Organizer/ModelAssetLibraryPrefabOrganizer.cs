using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using CJUtils;
using HierarchyBuilder = ModelAssetLibraryHierarchyBuilder;

public static class ModelAssetLibraryPrefabOrganizer {

    /// <summary> Data relevant to a folder category, namely, name and file contents; </summary>
    public class CategoryData {
        /// <summary> Parsed name of this category; </summary>
        public string name;
        /// <summary> ID list of all prefabs filed under this category;
        /// <br></br> Note that prefab categories is based on model category; </summary>
        public List<string> prefabIDs;
        /// <summary> ID list of all models filed under this category; </summary>
        public List<string> modelIDs;

        public CategoryData(string name) {
            this.name = name;
            prefabIDs = new List<string>();
            modelIDs = new List<string>();
        }
    } /// <summary> The folder path of the category selected in the GUI; </summary>
    public static string SelectedCategory { get; private set; }



    /// <summary> Maps a folder path to its category data; </summary>
    public static Dictionary<string, CategoryData> CategoryMap { get; private set; }

    /// <summary> Data used to draw prefab cards; </summary>
    public class PrefabCardData {
        /// <summary> Root gameObject atop the prefab file hierarchy; </summary>
        public GameObject rootObject;
        /// <summary> Asset preview of the prefab; </summary>
        public Texture2D preview;

        public PrefabCardData(GameObject rootObject, Texture2D preview) {
            this.rootObject = rootObject;
            this.preview = preview;
        }
    } /// <summary> Dictionary mapping path keys to the corresponding Prefab Card Data; </summary>
    private static Dictionary<string, PrefabCardData> PrefabCardMap;

    /// <summary> Selection group that may be passed to the DragAndDrop class; </summary>
    private static List<Object> dragSelectionGroup;
    /// <summary> Whether the mouse hovered over a button in the current frame; </summary>
    private static bool mouseOverButton;

    /// <summary> Sorting Modes for the Prefab Organizer; </summary>
    public enum PrefabSortMode {
        /// <summary> The prefabs cards will be drawn in alphabetical order; </summary>
        Name,
        /// <summary> The prefab cards will be grouped by parent model; </summary>
        Model
    } /// <summary> Sorting Mode selected on the Prefab Organizer GUI; </summary>
    public static PrefabSortMode SortMode { get; private set; }

    public static void BuildCategoryMap() {
        Dictionary<string, HierarchyBuilder.FolderData> folderMap;
        if (HierarchyBuilder.FolderMap == null) folderMap = HierarchyBuilder.FolderMap;
        else folderMap = HierarchyBuilder.BuildFolderMap(ModelAssetLibrary.RootAssetPath);
        CategoryMap = new Dictionary<string, CategoryData>();
        foreach (KeyValuePair<string, HierarchyBuilder.FolderData> kvp in folderMap) {
            CategoryMap[kvp.Key] = new CategoryData(kvp.Value.name);
            foreach (string modelPath in kvp.Value.files) {
                CategoryMap[kvp.Key].modelIDs.Add(AssetDatabase.AssetPathToGUID(modelPath));
            } foreach (string modelID in CategoryMap[kvp.Key].modelIDs) {
                CategoryMap[kvp.Key].prefabIDs.AddRange(ModelAssetLibrary.ModelDataDict[modelID].prefabIDList);
            }
        }
    }

    public static void SetSelectedCategory(string path) {
        dragSelectionGroup = new List<Object>();
        LoadCategoryData(path);
        SelectedCategory = path;
    }

    public static void FlushCategoryData() {
        if (PrefabCardMap != null) {
            foreach (PrefabCardData data in PrefabCardMap.Values) {
                if (data != null && data.preview != null) {
                    Object.DestroyImmediate(data.preview);
                }
            } PrefabCardMap = null;
        } SelectedCategory = null;
        dragSelectionGroup = null;
        SortMode = 0;
    }

    public static void LoadCategoryData(string path) {
        if (PrefabCardMap == null) PrefabCardMap = new Dictionary<string, PrefabCardData>();
        foreach (string prefabID in CategoryMap[path].prefabIDs) {
            if (PrefabCardMap.ContainsKey(prefabID)) continue;
            string assetPath = ModelAssetLibrary.PrefabDataDict[prefabID].path;
            GameObject rootObject = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            Texture2D preview = AssetPreview.GetAssetPreview(rootObject);
            while (preview == null) {
                preview = AssetPreview.GetAssetPreview(rootObject);
            } preview.hideFlags = HideFlags.HideAndDontSave;
            PrefabCardMap[prefabID] = new PrefabCardData(rootObject, preview);
        } dragSelectionGroup = new List<Object>();
    }


    public static void SetSortMode(PrefabSortMode sortMode) {
        /// Originally I did a thing or two here, but turns out I don't want to anymore;
        /// So I'll leave this method here anyways <3
        SortMode = sortMode;
    }

    public static void DrawPrefabOrganizerToolbar() {
        GUI.enabled = false;
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbarButton)) {
            if (SelectedCategory != null) GUI.enabled = true;
            GUILayout.Label("Sort By:", new GUIStyle(UIStyles.ToolbarText) { margin = new RectOffset(0, 20, 1, 0) }, GUILayout.Width(110));
        } if (GUILayout.Button("Name", SortMode == PrefabSortMode.Name
                                       ? UIStyles.SelectedToolbar : EditorStyles.toolbarButton, GUILayout.MinWidth(140), GUILayout.ExpandWidth(true))) {
            SetSortMode(PrefabSortMode.Name);
        } if (GUILayout.Button("Model", SortMode == PrefabSortMode.Model
                                        ? UIStyles.SelectedToolbar : EditorStyles.toolbarButton, GUILayout.MinWidth(140), GUILayout.ExpandWidth(true))) {
            SetSortMode(PrefabSortMode.Model);
        } GUILayout.FlexibleSpace();
        EditorGUILayout.TextField("", EditorStyles.toolbarSearchField, GUILayout.MinWidth(140));
        GUI.enabled = true;
    }

    public static void ShowSelectedCategory() {

        if (SelectedCategory == null) {
            EditorUtils.DrawScopeCenteredText("Prefabs stored in the Selected Category will be displayed here;");
            return;
        }

        using (new EditorGUILayout.HorizontalScope(GUI.skin.box)) {
            EditorUtils.DrawSeparatorLines(SelectedCategory.IsolatePathEnd("\\/"), true);
        }

        using (new EditorGUILayout.HorizontalScope()) {
            bool noItems = CategoryMap[SelectedCategory].prefabIDs.Count == 0;
            if (noItems) GUILayout.FlexibleSpace();
            switch (SortMode) {
                case PrefabSortMode.Name:
                    if (noItems) {
                        GUILayout.FlexibleSpace();
                        using (new EditorGUILayout.VerticalScope(GUI.skin.box)) {
                            GUILayout.Label("There are no prefabs under this category;");
                        } GUILayout.FlexibleSpace();
                    } DrawPrefabCards();
                    break;
                case PrefabSortMode.Model:
                    foreach (string modelID in CategoryMap[SelectedCategory].modelIDs) DrawModelColumn(modelID);
                    break;
            } if (noItems) GUILayout.FlexibleSpace();
        }
    }

    private static void DrawPrefabCards() {
        using (new EditorGUILayout.VerticalScope()) {
            List<string> prefabIDs = CategoryMap[SelectedCategory].prefabIDs;
            int amountPerRow = 3;
            for (int i = 0; i < Mathf.CeilToInt((float) prefabIDs.Count / amountPerRow); i++) {
                using (new EditorGUILayout.HorizontalScope(GUI.skin.box)) {
                    for (int j = i * amountPerRow; j < Mathf.Min((i + 1) * amountPerRow, prefabIDs.Count); j++) {
                        DrawPrefabCard(prefabIDs[j]);
                    }
                }
            } DeselectionCheck();
        } 
    }

    private static void DrawPrefabCard(string prefabID) {
        PrefabCardData data = PrefabCardMap[prefabID];
        bool objectInSelection = dragSelectionGroup.Contains(data.rootObject);
        if (objectInSelection) GUI.color = UIColors.DarkBlue;
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox, GUILayout.MaxWidth(200), GUILayout.Height(85))) {
            GUI.color = Color.white;
            DrawDragAndDropPreview(prefabID, objectInSelection);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandHeight(true))) {
                GUILayout.FlexibleSpace();
                using (new EditorGUILayout.HorizontalScope(GUI.skin.box)) {
                    string name = ModelAssetLibrary.PrefabDataDict[prefabID].path.IsolatePathEnd("\\/").RemovePathEnd(".");
                    GUILayout.Label(name, UIStyles.CenteredLabelBold);
                } using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
                    GUILayout.Button("Open Library", GUILayout.MaxHeight(24));
                    using (new EditorGUILayout.HorizontalScope()) {
                        if (GUILayout.Button(new GUIContent(EditorUtils.FetchIcon("d_PrefabModel On Icon")), GUILayout.MaxWidth(45), GUILayout.MaxHeight(24))) {
                            EditorUtils.OpenProjectWindow();
                            EditorGUIUtility.PingObject(AssetImporter.GetAtPath(ModelAssetLibrary
                                                                                .ModelDataDict[ModelAssetLibrary
                                                                                .PrefabDataDict[prefabID].modelID].path)); ;
                        } if (GUILayout.Button(new GUIContent(EditorUtils.FetchIcon("d_PrefabVariant On Icon")), GUILayout.MaxWidth(45), GUILayout.MaxHeight(24))) {
                            EditorUtils.OpenProjectWindow();
                            EditorGUIUtility.PingObject(data.rootObject);
                        }
                    }
                } GUILayout.FlexibleSpace();
            }
        }
    }

    private static void DrawDragAndDropPreview(string prefabID, bool objectInSelection) {
        PrefabCardData data = PrefabCardMap[prefabID];
        using (new EditorGUILayout.VerticalScope(GUI.skin.box)) {
            Rect buttonRect = GUILayoutUtility.GetRect(80, 80, GUILayout.ExpandWidth(false));
            if (buttonRect.Contains(Event.current.mousePosition)) {
                mouseOverButton = true;
                bool mouseDown = Event.current.type == EventType.MouseDown;
                bool mouseDrag = Event.current.type == EventType.MouseDrag;
                bool leftClick = Event.current.button == 0;
                bool rightClick = Event.current.button == 1;
                if (Event.current.shift) {
                    if ( objectInSelection) {
                        if (mouseDown || (mouseDrag && rightClick)) dragSelectionGroup.Remove(data.rootObject);
                    } else if (mouseDown || (mouseDrag && leftClick)) dragSelectionGroup.Add(data.rootObject);
                } else if (mouseDown && leftClick) {
                    DragAndDrop.PrepareStartDrag();
                    DragAndDrop.StartDrag("Dragging");
                    DragAndDrop.objectReferences = dragSelectionGroup.Count > 1
                                                   ? dragSelectionGroup.ToArray() : new Object[] { data.rootObject };
                    DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                }
            } GUI.Label(buttonRect, PrefabCardMap[prefabID].preview, GUI.skin.button);
        }
    }

    private static void DeselectionCheck() {
        if (!mouseOverButton && Event.current.type == EventType.MouseDown && !Event.current.shift
            && Event.current.button == 0 && dragSelectionGroup.Count > 0) dragSelectionGroup.Clear();
        mouseOverButton = false;
    }

    private static void DrawModelColumn(string modelID) {
        using (new EditorGUILayout.VerticalScope(GUI.skin.box)) {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {

            }
        }
    }
}