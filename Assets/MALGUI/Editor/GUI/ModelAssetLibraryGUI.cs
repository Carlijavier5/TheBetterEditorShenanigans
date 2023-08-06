using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using CJUtils;

/// <summary> Main GUI of the Model Asset Library;
/// <br></br> Interfaces with the user and handles tool loading and saving procedures; </summary>
public class ModelAssetLibraryGUI : EditorWindow {

    [MenuItem("Tools/Model Asset Library")]
    public static void ShowWindow() {
        if (HasOpenInstances<ModelAssetLibraryGUI>()) MainGUI.Close();
        ModelAssetLibraryConfigurationCore.LoadConfig();
        if (string.IsNullOrWhiteSpace(ModelAssetLibrary.RootAssetPath)) {
            ModelAssetLibraryConfigurationGUI.ShowWindow();
            return;
        } MainGUI = GetWindow<ModelAssetLibraryGUI>("Model Asset Library", typeof(ModelAssetLibraryConfigurationGUI));
        if (HasOpenInstances<ModelAssetLibraryConfigurationGUI>()) {
            ModelAssetLibraryConfigurationGUI.ConfigGUI.Close();
        }
    }

    #region | LibraryGUI-only variables |

    /// <summary> Reference to the active GUI Window; </summary>
    public static ModelAssetLibraryGUI MainGUI { get; private set; }

    #region | Hierachy Variables |
    /// <summary> Subfolder and File Paths + Foldout Scope of a folder in the model hierarchy; </summary>
    private class FolderData {
        public List<string> subfolders;
        public List<string> files;
        public bool foldout = true;
    } /// <summary> Dictionary of folders in the hierarchy with their respective information </summary>
    private Dictionary<string, FolderData> folderDict;

    /// <summary> Sorted list of all identified files for the search function; </summary>
    private List<string> fileList;

    /// <summary> Currently selected asset in the hierarchy; </summary>
    private string selectedFile;

    /// GUI variables;
    private string searchString;
    private Vector2 leftScrollPos;
    private Vector2 rightScrollPos;

    #endregion

    #region Asset Data Variables

    public enum SectionType {
        None,
        Model,
        Meshes,
        Materials,
        Prefabs,
        Rig,
        Animations,
        Skeleton
    } private static SectionType activeSection = SectionType.None;

    public enum AssetMode {
        Model,
        Animation
    } private AssetMode assetMode = AssetMode.Model;

    #endregion

    #endregion

    void OnEnable() {
        /// Initialize external scripts;
        ModelAssetLibraryConfigurationCore.LoadConfig();
        ModelAssetLibrary.Refresh();
        ModelAssetLibraryReader.FlushAssetData();

        /// Initialiaze internal data;
        folderDict = new Dictionary<string, FolderData>();
        fileList = new List<string>();
        BuildFolderDictionary(ModelAssetLibrary.RootAssetPath);
        fileList.Sort((name1, name2) => name1.IsolatePathEnd("\\/").CompareTo(name2.IsolatePathEnd("\\/")));
    }

    void OnDisable() {
        ModelAssetLibraryReader.FlushAssetData();
    }

    void OnFocus() {
        if (HasOpenInstances<ModelAssetLibraryGUI>()
            && MainGUI == null) MainGUI = GetWindow<ModelAssetLibraryGUI>();
    }

    void OnGUI() {
        using (new EditorGUILayout.HorizontalScope()) {
            using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(200), GUILayout.MaxWidth(220))) {
                using (new EditorGUILayout.HorizontalScope(UIStyles.PaddedToolbar)) {
                    searchString = EditorGUILayout.TextField(searchString, EditorStyles.toolbarSearchField);
                } using (var leftScope = new EditorGUILayout.ScrollViewScope(leftScrollPos, 
                                                                             false, true, GUI.skin.horizontalScrollbar, 
                                                                             GUI.skin.verticalScrollbar, UIStyles.PaddedScrollView)) {
                    leftScrollPos = leftScope.scrollPosition;
                    if (string.IsNullOrWhiteSpace(searchString)) {
                        DrawDictionary(ModelAssetLibrary.RootAssetPath);
                    } else DrawSearchQuery(searchString);
                }
            }
            using (new GUILayout.VerticalScope()) {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)) {
                    DrawToolbarButtons();
                } using (var rightScope = new EditorGUILayout.ScrollViewScope(rightScrollPos, UIStyles.MorePaddingScrollView)) {
                    rightScrollPos = rightScope.scrollPosition;
                    ShowSelectedAssetInterface();
                }
            }
        }
    }

    #region | Directory Hierarchy Interface |

    /// <summary>
    /// Iterates through the directories in the target path to build a dictionary tree;
    /// <br></br> This method is recursive and will traverse the full depth of the target folder hierarchy;
    /// </summary>
    /// <param name="path">The path to the root folder where the search should begin;</param>
    private void BuildFolderDictionary(string path) {
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

    private void DrawSearchQuery(string searchString) {
        List<string> filteredFileList = fileList.FindAll((str) => str.Contains(searchString));
        foreach (string file in filteredFileList) DrawFileButton(file);
    }

    /// <summary>
    /// Draws the folder and file hierarchy on the left-hand interface;
    /// </summary>
    /// <param name="path"> Path to the root folder where the hierarchy begins;
    /// <br></br> Note: The root folder path will be included in the hierarchy; </param>
    private void DrawDictionary(string path) {

        folderDict[path].foldout = EditorGUILayout.Foldout(folderDict[path].foldout, path.IsolatePathEnd("/\\"));
        EditorGUI.indentLevel++;

        if (folderDict[path].foldout) {
            foreach (string subfolder in folderDict[path].subfolders) {
                DrawDictionary(subfolder);
                EditorGUI.indentLevel--;
            } foreach (string file in folderDict[path].files) DrawFileButton(file);
        } 
    }

    /// <summary>
    /// Draw a button corresponding to model file in the hierarchy;
    /// </summary>
    /// <param name="file"> Path to the file; </param>
    private void DrawFileButton(string file) {
        GUIStyle buttonStyle = file == selectedFile ? UIStyles.HButtonSelected : UIStyles.HButton;
        string extension = file.IsolatePathEnd(".");
        string fileName = file.IsolatePathEnd("\\/").Replace(extension, extension.ToUpper());
        float width = EditorUtils.MeasureTextWidth(fileName, GUI.skin.font);
        if (GUILayout.Button(fileName, buttonStyle, GUILayout.Width(width + 14))) SetSelectedAsset(file);
    }

    #endregion

    #region | Selected Asset Interface |

    /// <summary>
    /// Draws the toolbar buttons depending on the current Asset Mode;
    /// </summary>
    private void DrawToolbarButtons() {
        switch (assetMode) {
            case AssetMode.Model:
                DrawToolbarButton(SectionType.Model, 72, 245);
                DrawToolbarButton(SectionType.Meshes, 72, 245);
                DrawToolbarButton(SectionType.Materials, 72, 245);
                DrawToolbarButton(SectionType.Prefabs, 112, 245);
                break;
            case AssetMode.Animation:
                DrawToolbarButton(SectionType.Rig, 66, 245);
                DrawToolbarButton(SectionType.Animations, 82, 245);
                DrawToolbarButton(SectionType.Skeleton, 72, 245);
                break;
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField("Asset Mode:", UIStyles.ToolbarText, GUILayout.Width(110));
        AssetMode newAssetMode = (AssetMode) EditorGUILayout.EnumPopup(assetMode, UIStyles.ToolbarPaddedPopUp, GUILayout.MinWidth(100), GUILayout.MaxWidth(180));
        if (assetMode != newAssetMode) SetSelectedAssetMode(newAssetMode);
        if (GUILayout.Button(EditorUtils.FetchIcon("_Popup"), EditorStyles.toolbarButton, GUILayout.MinWidth(32), GUILayout.MaxWidth(48))) {
            ModelAssetLibraryConfigurationGUI.ShowWindow();
        }
    }

    /// <summary>
    /// Draws a toolbar button for a given section;
    /// </summary>
    /// <param name="sectionType"></param>
    /// <param name="minWidth"></param>
    /// <param name="maxWidth"></param>
    private void DrawToolbarButton(SectionType sectionType, float minWidth, float maxWidth) {
        GUIStyle buttonStyle = sectionType == activeSection ? UIStyles.SelectedToolbar : EditorStyles.toolbarButton;
        if (GUILayout.Button(System.Enum.GetName(typeof(SectionType), sectionType), 
                             buttonStyle, GUILayout.MinWidth(minWidth), GUILayout.MaxWidth(maxWidth))) {
            SetSelectedSection(sectionType);
        }
    }

    /// <summary>
    /// Draws data from the currently selected asset on the right-hand interface;
    /// </summary>
    private void ShowSelectedAssetInterface() {
        if (selectedFile == null) {
            EditorUtils.DrawScopeCenteredText("Selected Asset Data will be displayed here;");
        } else {
            ModelAssetLibraryReaderGUI.ShowSelectedSection(activeSection);
        }
    }

    /// <summary>
    /// Set the currently selected asset;
    /// </summary>
    /// <param name="path"> Path to the selected asset; </param>
    private void SetSelectedAsset(string path) {
        if (selectedFile != path) {
            selectedFile = path;
            ModelAssetLibraryReader.FlushAssetData();
            ModelAssetLibraryReader.LoadSelectedAsset(path);
            if (activeSection == 0) activeSection = SectionType.Model;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    private void SetSelectedAssetMode(AssetMode assetMode) {
        switch (assetMode) {
            case AssetMode.Model:
                SetSelectedSection(SectionType.Model);
                break;
            case AssetMode.Animation:
                SetSelectedSection(SectionType.Rig);
                break;
        } this.assetMode = assetMode;
    }

    /// <summary>
    /// Sets the GUI's selected Reader Section;
    /// </summary>
    /// <param name="sectionType"></param>
    public static void SetSelectedSection(SectionType sectionType) {
        if (activeSection != sectionType) {
            activeSection = sectionType;
            ModelAssetLibraryReaderGUI.RefreshSections();
        }
    }

    #endregion
}