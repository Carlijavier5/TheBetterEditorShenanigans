using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using CJUtils;
using ModelReader = ModelAssetLibraryModelReader;
using ModelReaderGUI = ModelAssetLibraryModelReaderGUI;
using DirectoryBuilder = ModelAssetLibraryDirectoryBuilder;

/// <summary> Main GUI of the Model Asset Library;
/// <br></br> Connects a number of tools together in a single window;
/// </summary>
public class ModelAssetLibraryGUI : EditorWindow {

    /// <summary>
    /// Shows the Main Window of the Model Asset Library;
    /// </summary>
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

    /// <summary> Available tools to display in the library; </summary>
    public enum ToolMode {
        ModelReader,
        PrefabOrganizer,
        MaterialManager
    } /// <summary> The tool actively displayed within the library; </summary>
    private static ToolMode toolMode;

    private Vector2 toolScroll;

    #endregion

    void OnEnable() {
        ModelAssetLibraryConfigurationCore.LoadConfig();
        ModelAssetLibrary.Refresh();
        ModelReader.FlushAssetData();
        DirectoryBuilder.InitializeHierarchyData();
    }

    void OnDisable() {
        FlushGlobalToolData();
    }

    void OnFocus() {
        if (HasOpenInstances<ModelAssetLibraryGUI>()
            && MainGUI == null) MainGUI = GetWindow<ModelAssetLibraryGUI>();
    }

    void OnGUI() {
        using (new EditorGUILayout.HorizontalScope()) {
            using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(200), GUILayout.MaxWidth(220))) {
                DirectoryBuilder.DisplayToolDirectory(toolMode);
                DrawToolSelectionButtons();
            } using (new GUILayout.VerticalScope()) {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)) {
                    DrawToolbarButtons();
                } using (var rightScope = new EditorGUILayout.ScrollViewScope(toolScroll, UIStyles.MorePaddingScrollView)) {
                    toolScroll = rightScope.scrollPosition;
                    DrawActiveTool();
                }
            }
        }
    }

    /// <summary>
    /// Draws buttons to switch from one tool to another below the hierarchy;
    /// </summary>
    private void DrawToolSelectionButtons() {
        using (new EditorGUILayout.HorizontalScope(GUI.skin.box)) {
            if (toolMode == ToolMode.ModelReader) { GUI.color = UIColors.Blue; GUI.enabled = false; }
            if (GUILayout.Button(new GUIContent(EditorUtils.FetchIcon("d_PrefabModel Icon")),
                                 EditorStyles.toolbarButton, GUILayout.MaxHeight(20))) {
                SwitchActiveTool(ToolMode.ModelReader);
            } if (toolMode == ToolMode.PrefabOrganizer) { GUI.color = UIColors.Blue; GUI.enabled = false; }
            else { GUI.color = Color.white; GUI.enabled = true; }
            if (GUILayout.Button(new GUIContent(EditorUtils.FetchIcon("d_PrefabVariant Icon")),
                                   EditorStyles.toolbarButton, GUILayout.MaxHeight(20))) {
                SwitchActiveTool(ToolMode.PrefabOrganizer);
            } if (toolMode == ToolMode.MaterialManager) { GUI.color = UIColors.Blue; GUI.enabled = false; }
            else { GUI.color = Color.white; GUI.enabled = true; }
            if (GUILayout.Button(new GUIContent(EditorUtils.FetchIcon("d_Material Icon")),
                                   EditorStyles.toolbarButton, GUILayout.MaxHeight(20))) {
                SwitchActiveTool(ToolMode.MaterialManager);
            } GUI.color = Color.white; GUI.enabled = true;
        } 
    }

    /// <summary>
    /// Switch to a different tool;
    /// </summary>
    private void SwitchActiveTool(ToolMode newToolMode) {
        toolMode = newToolMode;
    }

    /// <summary>
    /// Draws the toolbar buttons depending on the currently selected tool;
    /// </summary>
    private void DrawToolbarButtons() {
        switch (toolMode) {
            case ToolMode.ModelReader:
                ModelReaderGUI.DrawModelReaderToolbar();
                break;
            case ToolMode.PrefabOrganizer:
                break;
            case ToolMode.MaterialManager:
                break;
        } if (GUILayout.Button(EditorUtils.FetchIcon("_Popup"), EditorStyles.toolbarButton, GUILayout.MinWidth(32), GUILayout.MaxWidth(48))) {
            ModelAssetLibraryConfigurationGUI.ShowWindow();
        }
    }

    /// <summary>
    /// Draws the currently selected tool on the right side of the window;
    /// </summary>
    private void DrawActiveTool() {
        switch (toolMode) {
            case ToolMode.ModelReader:
                ModelReaderGUI.ShowSelectedSection();
                break;
            case ToolMode.PrefabOrganizer:
                break;
            case ToolMode.MaterialManager:
                break;
        }
    }

    private void FlushGlobalToolData() {
        ModelReader.FlushAssetData();
    }
}