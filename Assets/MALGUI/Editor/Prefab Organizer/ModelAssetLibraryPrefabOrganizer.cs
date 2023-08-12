using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using CJUtils;
using HierarchyBuilder = ModelAssetLibraryHierarchyBuilder;

public static class ModelAssetLibraryPrefabOrganizer {

    public class CategoryData {
        public List<string> prefabIDs;
        public List<string> modelIDs;

        public CategoryData() {
            prefabIDs = new List<string>();
            modelIDs = new List<string>();
        }
    }
    public static string SelectedCategory { get; private set; }

    public static Dictionary<string, CategoryData> CategoryMap { get; private set; }

    public enum PrefabSortMode {
        Name,
        Model
    } public static PrefabSortMode SortMode { get; private set; }

    public static void BuildCategoryMap() {
        Dictionary<string, HierarchyBuilder.FolderData> folderMap;
        if (HierarchyBuilder.FolderMap == null) folderMap = HierarchyBuilder.FolderMap;
        else folderMap = HierarchyBuilder.BuildFolderMap(ModelAssetLibrary.RootAssetPath);
        CategoryMap = new Dictionary<string, CategoryData>();
        foreach (KeyValuePair<string, HierarchyBuilder.FolderData> kvp in folderMap) {
            CategoryMap[kvp.Key] = new CategoryData();
            foreach (string modelPath in kvp.Value.files) {
                CategoryMap[kvp.Key].modelIDs.Add(AssetDatabase.AssetPathToGUID(modelPath));
            } foreach (string modelID in kvp.Value.files) {
                CategoryMap[kvp.Key].prefabIDs.AddRange(ModelAssetLibrary.ModelDataDict[modelID].prefabIDList);
            }
        } 
    }

    public static void SetSelectedCategory(string path) {
        SelectedCategory = path;
    }

    public static void DrawPrefabOrganizerToolbar() {
        GUI.enabled = false;
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbarButton)) {
            GUI.enabled = true;
            GUILayout.Label("Sort By:", new GUIStyle(UIStyles.ToolbarText) { margin = new RectOffset(0, 20, 1, 0) }, GUILayout.Width(110));
        } GUILayout.Button("Name", EditorStyles.toolbarButton, GUILayout.MinWidth(140), GUILayout.ExpandWidth(true));
        GUILayout.Button("Model", EditorStyles.toolbarButton, GUILayout.MinWidth(140), GUILayout.ExpandWidth(true));
        GUILayout.FlexibleSpace();
        EditorGUILayout.TextField("", EditorStyles.toolbarSearchField, GUILayout.MinWidth(140));
    }
}