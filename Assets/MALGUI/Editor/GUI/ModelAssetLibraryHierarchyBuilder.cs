using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using CJUtils;
using static ModelAssetLibraryGUI;
using ModelReader = ModelAssetLibraryModelReader;
using PrefabOrganizer = ModelAssetLibraryPrefabOrganizer;
using MaterialManager = ModelAssetLibraryMaterialManager;

/// <summary> Core class of the Model Asset Library;
/// <br></br> Reads the folder directory and generates interactable Hierarchy Previews; </summary>
public class ModelAssetLibraryHierarchyBuilder {

    /// <summary> Subfolder and File Paths + Foldout Scope of a folder in the model hierarchy; </summary>
    public class FolderData {
        public string name;
        public List<string> subfolders;
        public List<string> models;
        public List<string> materials;
        public bool foldout = true;
    } /// <summary> Dictionary that maps each folder path in the hierarchy to some useful information; </summary>
    public static Dictionary<string, FolderData> FolderMap { get; private set; }

    /// <summary> Sorted list of all identified models for the search function; </summary>
    private static List<string> modelList;

    /// <summary> Sorted list of all identified model-containing folders for the search function; </summary>
    private static List<string> folderList;

    /// <summary> Sorted list of all identfied materials for the search function; </summary>
    private static List<string> materialList;

    /// <summary> Currently selected model path in the hierarchy; </summary>
    private static string SelectedModelPath { get { return ModelReader.Model != null ? ModelReader.Model.assetPath : null; } }

    /// <summary> Currently selected category path in the hierarchy; </summary>
    private static string SelectedCategoryPath { get { return PrefabOrganizer.SelectedCategory; } }

    private static string SelectedMaterialPath { get { return MaterialManager.EditedMaterial != null ? MaterialManager.EditedMaterial.path : null; } }

    /// GUI variables;
    private static string searchString;

    /// <summary>
    /// Loads the data required to generate Hierarchy Previews;
    /// </summary>
    public static void InitializeHierarchyData() {
        modelList = new List<string>();
        folderList = new List<string>();
        materialList = new List<string>();
        FolderMap = BuildFolderMap(ModelAssetLibrary.RootAssetPath, false);
        modelList.Sort((name1, name2) => AlnumSort(name1, name2));
        folderList.Sort((name1, name2) => AlnumSort(name1, name2));
        materialList.Sort((name1, name2) => AlnumSort(name1, name2));
    }

    /// <summary>
    /// Alphanumerical sort comparison expression;
    /// </summary>
    /// <param name="name1"> First string; </param>
    /// <param name="name2"> Second string; </param>
    /// <returns> A comparison integer between two strings based on lexicographical order; </returns>
    private static int AlnumSort(string name1, string name2) => name1.IsolatePathEnd("\\/").CompareTo(name2.IsolatePathEnd("\\/"));

    /// <summary>
    /// Unloads all static data;
    /// </summary>
    public static void FlushHierarchyData() {
        modelList = null;
        folderList = null;
        materialList = null;
        FolderMap = null;
    }

    /// <summary>
    /// Iterates through the directories in the target path to build a dictionary tree;
    /// <br></br> This method is recursive and will traverse the full depth of the target folder hierarchy;
    /// </summary>
    /// <param name="path"> The path to the root folder where the search should begin; </param>
    /// <param name="externalCall"> Whether the function is called outside of the Hierarchy Builder; </param>
    /// <param name="newFolderMap"> Recursive variable; </param>
    public static Dictionary<string, FolderData> BuildFolderMap(string path, bool externalCall = true, Dictionary<string, FolderData> newFolderMap = null) {
        path = path.Replace('\\', '/');
        if (newFolderMap == null) newFolderMap = new Dictionary<string, FolderData>();
        newFolderMap[path] = new FolderData();
        List<string> subfolders = new List<string>(AssetDatabase.GetSubFolders(path));
        List<string> models = new List<string>(ModelAssetLibrary.FindAssets(path, ModelAssetLibrary.ModelFileExtensions));
        List<string> materials = new List<string>(ModelAssetLibrary.FindAssets(path, new string[] { "MAT" }));
        for (int i = 0; i < models.Count; i++) models[i] = models[i].Replace('\\', '/');
        FolderData folderEntry = newFolderMap[path];
        folderEntry.name = path.IsolatePathEnd("\\/");
        folderEntry.subfolders = subfolders;
        folderEntry.models = models;
        folderEntry.materials = materials;
        foreach (string subfolder in subfolders) {
            BuildFolderMap(subfolder, externalCall, newFolderMap);
        } if (!externalCall) {
            modelList.AddRange(models);
            if (models.Count > 0) folderList.Add(path);
            materialList.AddRange(materials);
        } return newFolderMap;
    }

    /// <summary>
    /// Sets a Selected Model in the Model Reader;
    /// </summary>
    /// <param name="path"> Path of the Model Importer; </param>
    private static void SetSelectedModel(string path) { if (SelectedModelPath != path) ModelReader.SetSelectedModel(path); }

    /// <summary>
    /// Sets a Selected Category in the Prefab Organizer;
    /// </summary>
    /// <param name="path"> Path of the folder; </param>
    private static void SetSelectedPrefabFolder(string path) { if (SelectedCategoryPath != path) PrefabOrganizer.SetSelectedCategory(path); }

    /// <summary>
    /// Sets the Edited Material in the Material Manager;
    /// </summary>
    /// <param name="path"> Path to the material; </param>
    private static void SetSelectedMaterial(string path) { if (SelectedMaterialPath != path) MaterialManager.SetEditedMaterial(path); }

    /// <summary>
    /// Generates a Results List using the Search String obtained through the Hierarchy Search Bar; 
    /// </summary>
    /// <param name="searchString"> Search String to process; </param>
    /// <returns> A list containing all matching results depending on the active tool; </returns>
    private static List<string> GetSearchQuery(string searchString, ToolMode toolMode) {
        switch (toolMode) {
            case ToolMode.ModelReader:
                return modelList.FindAll((str) => str.Contains(searchString));
            case ToolMode.PrefabOrganizer:
                return folderList.FindAll((str) => str.Contains(searchString));
            case ToolMode.MaterialManager:
                return null;
        } return null;
    }

    // GUI

    /// <summary>
    /// Show a Hierarchy Preview applicable to the current tool;
    /// </summary>
    public static void DisplayToolDirectory(ToolMode toolMode) {

        switch (toolMode) {
            case ToolMode.ModelReader:
                DisplayModelReaderDirectory();
                break;
            case ToolMode.PrefabOrganizer:
                DisplayPrefabOrganizerDirectory();
                break;
            case ToolMode.MaterialManager:
                DisplayMaterialManagerDirectory();
                break;
        }
    }

    /// <summary>
    /// Display a Hierarchy Preview suitable for the Model Reader;
    /// <br></br> Shows a List of filtered buttons if there's an active search query;
    /// </summary>
    private static void DisplayModelReaderDirectory() {
        if (string.IsNullOrWhiteSpace(searchString)) {
            DrawModelDictionary(ModelAssetLibrary.RootAssetPath);
        } else DrawSearchQuery(searchString, ToolMode.ModelReader);
    }

    /// <summary>
    /// Display a Hierarchy Preview suitable for the Prefab Organizer;
    /// <br></br> Shows a List of filtered buttons if there's an active search query;
    /// </summary>
    private static void DisplayPrefabOrganizerDirectory() {
        if (string.IsNullOrWhiteSpace(searchString)) {
            DrawPrefabFolderDictionary(ModelAssetLibrary.RootAssetPath);
        } else DrawSearchQuery(searchString, ToolMode.PrefabOrganizer);
    }

    private static void DisplayMaterialManagerDirectory() {
        switch (MaterialManager.ActiveSection) {
            case MaterialManager.SectionType.Editor:
                if (string.IsNullOrWhiteSpace(searchString)) {
                    DrawMaterialDictionary(ModelAssetLibrary.RootAssetPath);
                } else DrawSearchQuery(searchString, ToolMode.MaterialManager);
                GUI.enabled = true;
                break;
            case MaterialManager.SectionType.Creator:
            case MaterialManager.SectionType.Organizer:
                GUI.enabled = false;
                goto case MaterialManager.SectionType.Editor;
            case MaterialManager.SectionType.Replacer:
                
                break;
        }
    }

    #region | Search Bar |

    /// <summary>
    /// Draws the Search Bar atop the Hierarchy Preview;
    /// </summary>
    public static void DrawSearchBar() {
        using (new EditorGUILayout.HorizontalScope(UIStyles.PaddedToolbar)) {
            searchString = EditorGUILayout.TextField(searchString, EditorStyles.toolbarSearchField);
        }
    }

    /// <summary>
    /// Draws contents based on the current Search String;
    /// </summary>
    /// <param name="searchString"> Search String to filter the contents with; </param>
    /// <param name="toolMode"> Active tool type; </param>
    private static void DrawSearchQuery(string searchString, ToolMode toolMode) {
        switch (toolMode) {
            case ToolMode.ModelReader:
                List<string> filteredFileList = GetSearchQuery(searchString, toolMode);
                foreach (string file in filteredFileList) DrawModelButton(file);
                break;
            case ToolMode.PrefabOrganizer:
                List<string> filteredFolderList = GetSearchQuery(searchString, toolMode);
                foreach (string folder in filteredFolderList) DrawPrefabFolderButton(folder, false);
                break;
        }
    }

    #endregion

    /// <summary>
    /// Perform a search through subfolders to identify any folder containing models or materials;
    /// <br></br> Used to determine whether a folder/foldout is worth drawing;
    /// </summary>
    /// <param name="path"> Path to begin the subfolder search in; </param>
    /// <param name="searchModels"> Whether to search for models or materials; </param>
    /// <returns> Whether a folder containing models or materials was found; </returns>
    private static bool PerformAssetSearch(string path, bool searchModels = true) {
        foreach (string folder in FolderMap[path].subfolders) {
            int countParameter = searchModels ? FolderMap[folder].models.Count : FolderMap[folder].materials.Count;
            if (countParameter > 0) return true;
            else if (FolderMap[folder].subfolders.Count > 0) return PerformAssetSearch(folder);
        } return false;
    }

    /// <summary>
    /// Draw a conditional foldout based on folder data;
    /// </summary>
    /// <param name="path"> Path to the foldout folder to draw; </param>
    /// <param name="data"> Data pertaining to the folder to draw; </param>
    /// <param name="marginCondition"> Whether the folder will fold out to show materials; </param>
    /// <returns></returns>
    private static bool DrawConditionalFoldout(string path, FolderData data, bool marginCondition) {
        GUIContent foldoutContent = new GUIContent(" " + path.IsolatePathEnd("/\\"),
                                        EditorUtils.FetchIcon(data.foldout ? "d_FolderOpened Icon" : "d_Folder Icon"));
        float width = EditorUtils.MeasureTextWidth(data.name, GUI.skin.font);
        return EditorGUILayout.Foldout(data.foldout, foldoutContent,
                                                     new GUIStyle(EditorStyles.foldoutHeader) {
                                                         fixedWidth = width + 48,
                                                         fixedHeight = 19,
                                                         margin = new RectOffset(0, 0, 0,
                                                         marginCondition && data.foldout ? 2 : 0)
                                                     });
    }

    /// <summary>
    /// Draws a folder + model hierarchy on the left-hand interface;
    /// </summary>
    /// <param name="path"> Path to the root folder where the hierarchy begins;
    /// <br></br> Note: The root folder path will be included in the hierarchy; </param>
    private static void DrawModelDictionary(string path) {
        FolderData data = FolderMap[path];
        bool hasModels = data.models.Count > 0;
        if (hasModels || (data.subfolders.Count > 0 && PerformAssetSearch(path))) {
            data.foldout = DrawConditionalFoldout(path, data, hasModels);
        } EditorGUI.indentLevel++;

        if (FolderMap[path].foldout) {
            foreach (string subfolder in FolderMap[path].subfolders) {
                DrawModelDictionary(subfolder);
                EditorGUI.indentLevel--;
            } foreach (string file in FolderMap[path].models) DrawModelButton(file);
        }
    }

    /// <summary>
    /// Draws a button corresponding to model file in the hierarchy;
    /// </summary>
    /// <param name="path"> Path to the file; </param>
    private static void DrawModelButton(string path) {
        bool selected = path == SelectedModelPath;
        GUIStyle buttonStyle = selected ? UIStyles.HButtonSelected : UIStyles.HButton;
        string extension = path.IsolatePathEnd(".");
        string fileName = path.IsolatePathEnd("\\/").Replace(extension, extension.ToUpper());
        float width = EditorUtils.MeasureTextWidth(fileName, GUI.skin.font);
        var data = ModelAssetLibraryExtManager.FetchExtData(AssetDatabase.AssetPathToGUID(path));
        GUIContent modelContent;
        Texture2D icon;
        if (data != null) {
            if (selected) icon = EditorUtils.FetchIcon(data.isModel ? "d_PrefabModel Icon" : "AvatarSelector");
            else icon = EditorUtils.FetchIcon(data.isModel ? "d_PrefabModel On Icon" : "AvatarMask On Icon");
        } else {
            if (selected) icon = EditorUtils.FetchIcon("d_ScriptableObject Icon");
            else icon = EditorUtils.FetchIcon("d_ScriptableObject On Icon");
        } modelContent = new GUIContent(fileName, icon);
        if (GUILayout.Button(modelContent, buttonStyle, GUILayout.Width(width + 29), GUILayout.Height(20))) SetSelectedModel(path);
    }

    /// <summary>
    /// Draws a folder hierarchy on the left-hand interface;
    /// </summary>
    /// <param name="path"> Path to the root folder where the hierarchy begins;
    /// <br></br> Note: The root folder path will be included in the hierarchy; </param>
    private static void DrawPrefabFolderDictionary(string path) {

        using (new EditorGUILayout.HorizontalScope()) {
            bool hasFiles = FolderMap[path].models.Count > 0;
            bool hasSubfolders = FolderMap[path].subfolders.Count > 0;
            GUIContent folderContent;
            if (hasFiles) {
                folderContent = new GUIContent("");
            } else folderContent = new GUIContent(path.IsolatePathEnd("\\/"),
                                                  EditorUtils.FetchIcon(FolderMap[path].foldout ? "d_FolderOpened Icon" : "d_Folder Icon"));
            bool worthShowing = hasSubfolders && PerformAssetSearch(path);
            if (worthShowing) {
                Rect rect = GUILayoutUtility.GetRect(0, 18, GUILayout.Width(13));
                FolderMap[path].foldout = EditorGUI.Foldout(rect, FolderMap[path].foldout, folderContent,
                                                                   new GUIStyle(EditorStyles.foldout) { stretchWidth = false });
            } if (hasFiles) DrawPrefabFolderButton(path, worthShowing && FolderMap[path].foldout);
        } EditorGUI.indentLevel++;

        if (FolderMap[path].foldout) {
            foreach (string subfolder in FolderMap[path].subfolders) {
                DrawPrefabFolderDictionary(subfolder);
                EditorGUI.indentLevel--;
            } 
        } 
    }

    /// <summary>
    /// Draws a button corresponding to a relevant folder in the hierarchy;
    /// </summary>
    /// <param name="path"> Path to the folder; </param>
    /// <param name="folderOpened"> Whether the foldout is active, so the Folder icon can reflect it; </param>
    private static void DrawPrefabFolderButton(string path, bool folderOpened) {
        GUIStyle buttonStyle = path == SelectedCategoryPath ? UIStyles.HFButtonSelected : UIStyles.HFButton;
        GUIContent folderContent = new GUIContent(path.IsolatePathEnd("\\/"), EditorUtils.FetchIcon(folderOpened ? "d_FolderOpened Icon" : "d_Folder Icon"));
        float width = EditorUtils.MeasureTextWidth(folderContent.text, GUI.skin.font);
        if (GUILayout.Button(folderContent, buttonStyle, GUILayout.Width(width + 34), GUILayout.Height(20))) SetSelectedPrefabFolder(path);
    }

    private static void DrawMaterialDictionary(string path) {
        FolderData data = FolderMap[path];
        bool hasMaterials = data.materials.Count > 0;
        if (hasMaterials || (data.subfolders.Count > 0 && PerformAssetSearch(path, false))) {
            FolderMap[path].foldout = DrawConditionalFoldout(path, data, hasMaterials);
        } EditorGUI.indentLevel++;

        if (FolderMap[path].foldout) {
            foreach (string subfolder in FolderMap[path].subfolders) {
                DrawMaterialDictionary(subfolder);
                EditorGUI.indentLevel--;
            } foreach (string materialPath in FolderMap[path].materials) DrawMaterialButton(materialPath);
        }
    }

    private static void DrawMaterialButton(string path) {
        bool selected = path == SelectedMaterialPath;
        GUIStyle buttonStyle = selected ? UIStyles.HButtonSelected : UIStyles.HButton;
        string pathName = path.IsolatePathEnd("\\/").RemovePathEnd(".");
        float width = EditorUtils.MeasureTextWidth(pathName, GUI.skin.font);
        GUIContent materialContent = new GUIContent(pathName, 
                                                    EditorUtils.FetchIcon(selected ? "d_Material Icon" : "d_Material On Icon"));
        if (GUILayout.Button(materialContent, buttonStyle, GUILayout.Width(width + 29), GUILayout.Height(20))) SetSelectedMaterial(path);
    }
}