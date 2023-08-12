using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using CJUtils;
using static ModelAssetLibraryGUI;

public static class ModelAssetLibraryDirectoryBuilder {

    /// <summary> Subfolder and File Paths + Foldout Scope of a folder in the model hierarchy; </summary>
    private class FolderData {
        public List<string> subfolders;
        public List<string> files;
        public bool foldout = true;
    } /// <summary> Dictionary of folders in the hierarchy with their respective information </summary>
    private static Dictionary<string, FolderData> folderDict;

    /// <summary> Sorted list of all identified files for the search function; </summary>
    private static List<string> fileList;

    /// <summary> Currently selected asset in the hierarchy; </summary>
    private static string selectedFile;

    /// GUI variables;
    private static string searchString;

    private static Vector2 directoryScroll; 

    public static void InitializeHierarchyData() {
        folderDict = new Dictionary<string, FolderData>();
        fileList = new List<string>();
        BuildFolderDictionary(ModelAssetLibrary.RootAssetPath);
        fileList.Sort((name1, name2) => name1.IsolatePathEnd("\\/").CompareTo(name2.IsolatePathEnd("\\/")));
    }

    /// <summary>
    /// Iterates through the directories in the target path to build a dictionary tree;
    /// <br></br> This method is recursive and will traverse the full depth of the target folder hierarchy;
    /// </summary>
    /// <param name="path">The path to the root folder where the search should begin;</param>
    private static void BuildFolderDictionary(string path) {
        folderDict[path] = new FolderData();
        List<string> subfolders = new List<string>(Directory.GetDirectories(path));
        List<string> files = new List<string>(ModelAssetLibrary.FindAssets(path, ModelAssetLibrary.ModelFileExtensions));
        if (subfolders.Count > 0 || files.Count > 0) {
            folderDict[path].subfolders = new List<string>(subfolders);
            folderDict[path].files = files;
            foreach (string subfolder in subfolders) {
                BuildFolderDictionary(subfolder);
            } fileList.AddRange(files);
        } else {
            folderDict[path.RemovePathEnd("\\/")].subfolders.Remove(path);
            folderDict.Remove(path);
        }
    }

    private static void SetSelectedModelAsset(string assetPath) {
        if (selectedFile != assetPath) {
            selectedFile = assetPath;
            ModelAssetLibraryModelReader.FlushAssetData();
            ModelAssetLibraryModelReader.LoadSelectedAsset(assetPath);
            ModelAssetLibraryModelReader.SetSelectedModel(assetPath);
        }
    }

    private static List<string> GetSearchQuery(string searchString) => fileList.FindAll((str) => str.Contains(searchString));

    /// GUI

    public static void DisplayToolDirectory(ToolMode toolMode) {

        DrawSearchBar();

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

    private static void DisplayModelReaderDirectory() {
        using (var leftScope = new EditorGUILayout.ScrollViewScope(directoryScroll, 
                                                                     false, true, GUI.skin.horizontalScrollbar, 
                                                                     GUI.skin.verticalScrollbar, UIStyles.PaddedScrollView)) {
            directoryScroll = leftScope.scrollPosition;
            if (string.IsNullOrWhiteSpace(searchString)) {
                DrawModelDictionary(ModelAssetLibrary.RootAssetPath);
            } else DrawSearchQuery(searchString);
        }
    }

    private static void DisplayPrefabOrganizerDirectory() {

    }

    private static void DisplayMaterialManagerDirectory() {

    }

    #region | Search Bar |

    private static void DrawSearchBar() {
        using (new EditorGUILayout.HorizontalScope(UIStyles.PaddedToolbar)) {
            searchString = EditorGUILayout.TextField(searchString, EditorStyles.toolbarSearchField);
        }
    }

    private static void DrawSearchQuery(string searchString) {
        List<string> filteredFileList = GetSearchQuery(searchString);
        foreach (string file in filteredFileList) DrawModelButton(file);
    }

    #endregion

    /// <summary>
    /// Draws the folder and file hierarchy on the left-hand interface;
    /// </summary>
    /// <param name="path"> Path to the root folder where the hierarchy begins;
    /// <br></br> Note: The root folder path will be included in the hierarchy; </param>
    private static void DrawModelDictionary(string path) {

        folderDict[path].foldout = EditorGUILayout.Foldout(folderDict[path].foldout, path.IsolatePathEnd("/\\"));
        EditorGUI.indentLevel++;

        if (folderDict[path].foldout) {
            foreach (string subfolder in folderDict[path].subfolders) {
                DrawModelDictionary(subfolder);
                EditorGUI.indentLevel--;
            } foreach (string file in folderDict[path].files) DrawModelButton(file);
        } 
    }

    /// <summary>
    /// Draw a button corresponding to model file in the hierarchy;
    /// </summary>
    /// <param name="file"> Path to the file; </param>
    private static void DrawModelButton(string file) {
        GUIStyle buttonStyle = file == selectedFile ? UIStyles.HButtonSelected : UIStyles.HButton;
        string extension = file.IsolatePathEnd(".");
        string fileName = file.IsolatePathEnd("\\/").Replace(extension, extension.ToUpper());
        float width = EditorUtils.MeasureTextWidth(fileName, GUI.skin.font);
        if (GUILayout.Button(fileName, buttonStyle, GUILayout.Width(width + 14))) SetSelectedModelAsset(file);
    }
}
