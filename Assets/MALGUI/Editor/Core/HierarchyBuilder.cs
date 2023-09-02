using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using CJUtils;
using static ModelAssetDatabase.ModelAssetDatabaseGUI;

namespace ModelAssetDatabase {

    /// <summary> Core class of the Model Asset Database;
    /// <br></br> Reads the folder directory and generates interactable Hierarchy Previews; </summary>
    public class HierarchyBuilder : BaseTool {

        private HierarchyTab[] tabs;

        /// <summary> Currently selected model path in the hierarchy; </summary>
        private string SelectedModelPath { get { return MainGUI.ModelReader.Model != null ? MainGUI.ModelReader.Model.assetPath : null; } }

        /// <summary> Currently selected category path in the hierarchy; </summary>
        private string SelectedFolderPath { get { return MainGUI.PrefabOrganizer.SelectedFolder; } }

        private string SelectedMaterialPath { get { return MainGUI.MaterialManager.EditedMaterial != null ? MainGUI.MaterialManager.EditedMaterial.path : null; } }

        /// GUI variables;
        private string searchString;

        #region | Initialization & Cleanup |

        /// <summary>
        /// Loads the data required to generate Hierarchy Previews;
        /// </summary>
        protected override void InitializeData() {
            tabs = new HierarchyTab[] {
                ToolTab.CreateTab<HierarchyTabModels>(this),
                ToolTab.CreateTab<HierarchyTabFolders>(this),
                ToolTab.CreateTab<HierarchyTabMaterials>(this),
            };
        }

        public override void FlushData() { }

        #endregion

        // GUI

        #region | Search Bar |

        /// <summary>
        /// Draws the Search Bar atop the Hierarchy Preview;
        /// </summary>
        public override void DrawToolbar() {
            using (new EditorGUILayout.HorizontalScope(UIStyles.PaddedToolbar)) {
                searchString = EditorGUILayout.TextField(searchString, EditorStyles.toolbarSearchField);
            }
        }

        /// <summary>
        /// Draws contents based on the current Search String;
        /// </summary>
        /// <param name="searchString"> Search String to filter the contents with; </param>
        /// <param name="toolMode"> Active tool type; </param>
        private void DrawSearchQuery(string searchString, ToolMode toolMode) {
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
        /// Show a Hierarchy Preview applicable to the current tool;
        /// </summary>
        public override void ShowGUI() {

            switch (MainGUI.ActiveTool) {
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
        private void DisplayModelReaderDirectory() {
            if (string.IsNullOrWhiteSpace(searchString)) {
                DrawModelDictionary(ModelAssetDatabase.RootAssetPath);
            } else DrawSearchQuery(searchString, ToolMode.ModelReader);
        }

        /// <summary>
        /// Display a Hierarchy Preview suitable for the Prefab Organizer;
        /// <br></br> Shows a List of filtered buttons if there's an active search query;
        /// </summary>
        private void DisplayPrefabOrganizerDirectory() {
            if (string.IsNullOrWhiteSpace(searchString)) {
                DrawPrefabFolderDictionary(ModelAssetDatabase.RootAssetPath);
            } else DrawSearchQuery(searchString, ToolMode.PrefabOrganizer);
        }

        private void DisplayMaterialManagerDirectory() {
            switch (MainGUI.MaterialManager.ActiveSection) {
                case MaterialManager.SectionType.Editor:
                    if (string.IsNullOrWhiteSpace(searchString)) {
                        DrawMaterialDictionary(ModelAssetDatabase.RootAssetPath);
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

        /// <summary>
        /// Perform a search through subfolders to identify any folder containing models or materials;
        /// <br></br> Used to determine whether a folder/foldout is worth drawing;
        /// </summary>
        /// <param name="path"> Path to begin the subfolder search in; </param>
        /// <param name="searchModels"> Whether to search for models or materials; </param>
        /// <returns> Whether a folder containing models or materials was found; </returns>
        private bool PerformAssetSearch(string path, bool searchModels = true) {
            foreach (string folder in folderMap[path].subfolders) {
                int countParameter = searchModels ? folderMap[folder].models.Count : folderMap[folder].materials.Count;
                if (countParameter > 0) return true;
                else if (folderMap[folder].subfolders.Count > 0) return PerformAssetSearch(folder);
            } return false;
        }

        /// <summary>
        /// Draw a conditional foldout based on folder data;
        /// </summary>
        /// <param name="path"> Path to the foldout folder to draw; </param>
        /// <param name="data"> Data pertaining to the folder to draw; </param>
        /// <param name="marginCondition"> Whether the folder will fold out to show materials; </param>
        /// <returns></returns>
        private static bool DrawConditionalFoldout(string path, ModelAssetDatabase.FolderData data, bool marginCondition) {
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
        private void DrawModelDictionary(string path) {
            ModelAssetDatabase.FolderData data = folderMap[path];
            bool hasModels = data.models.Count > 0;
            if (hasModels || (data.subfolders.Count > 0 && PerformAssetSearch(path))) {
                data.foldout = DrawConditionalFoldout(path, data, hasModels);
            } EditorGUI.indentLevel++;

            if (folderMap[path].foldout) {
                foreach (string subfolder in folderMap[path].subfolders) {
                    DrawModelDictionary(subfolder);
                    EditorGUI.indentLevel--;
                } foreach (string file in folderMap[path].models) DrawModelButton(file);
            }
        }

        /// <summary>
        /// Draws a button corresponding to model file in the hierarchy;
        /// </summary>
        /// <param name="path"> Path to the file; </param>
        private void DrawModelButton(string path) {
            bool selected = path == SelectedModelPath;
            GUIStyle buttonStyle = selected ? UIStyles.HButtonSelected : UIStyles.HButton;
            string extension = path.IsolatePathEnd(".");
            string fileName = path.IsolatePathEnd("\\/").Replace(extension, extension.ToUpper());
            float width = EditorUtils.MeasureTextWidth(fileName, GUI.skin.font);
            var data = ExtManager.FetchExtData(AssetDatabase.AssetPathToGUID(path));
            GUIContent modelContent;
            Texture2D icon;
            if (data != null) {
                if (selected) icon = EditorUtils.FetchIcon(data.isModel ? "d_PrefabModel Icon" : "AvatarSelector");
                else icon = EditorUtils.FetchIcon(data.isModel ? "d_PrefabModel On Icon" : "AvatarMask On Icon");
            } else {
                if (selected) icon = EditorUtils.FetchIcon("d_ScriptableObject Icon");
                else icon = EditorUtils.FetchIcon("d_ScriptableObject On Icon");
            } modelContent = new GUIContent(fileName, icon);
            if (GUILayout.Button(modelContent, buttonStyle, GUILayout.Width(width + 29), GUILayout.Height(20))) MainGUI.SetSelectedAsset(path);
        }

        /// <summary>
        /// Draws a folder hierarchy on the left-hand interface;
        /// </summary>
        /// <param name="path"> Path to the root folder where the hierarchy begins;
        /// <br></br> Note: The root folder path will be included in the hierarchy; </param>
        private void DrawPrefabFolderDictionary(string path) {

            using (new EditorGUILayout.HorizontalScope()) {
                bool hasFiles = folderMap[path].models.Count > 0;
                bool hasSubfolders = folderMap[path].subfolders.Count > 0;
                GUIContent folderContent;
                if (hasFiles) {
                    folderContent = new GUIContent("");
                } else folderContent = new GUIContent(path.IsolatePathEnd("\\/"),
                                                      EditorUtils.FetchIcon(folderMap[path].foldout ? "d_FolderOpened Icon" : "d_Folder Icon"));
                bool worthShowing = hasSubfolders && PerformAssetSearch(path);
                if (worthShowing) {
                    Rect rect = GUILayoutUtility.GetRect(0, 18, GUILayout.Width(13));
                    folderMap[path].foldout = EditorGUI.Foldout(rect, folderMap[path].foldout, folderContent,
                                                                       new GUIStyle(EditorStyles.foldout) { stretchWidth = false });
                } if (hasFiles) DrawPrefabFolderButton(path, worthShowing && folderMap[path].foldout);
            } EditorGUI.indentLevel++;

            if (folderMap[path].foldout) {
                foreach (string subfolder in folderMap[path].subfolders) {
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
        private void DrawPrefabFolderButton(string path, bool folderOpened) {
            GUIStyle buttonStyle = path == SelectedFolderPath ? UIStyles.HFButtonSelected : UIStyles.HFButton;
            GUIContent folderContent = new GUIContent(path.IsolatePathEnd("\\/"), EditorUtils.FetchIcon(folderOpened ? "d_FolderOpened Icon" : "d_Folder Icon"));
            float width = EditorUtils.MeasureTextWidth(folderContent.text, GUI.skin.font);
            if (GUILayout.Button(folderContent, buttonStyle, GUILayout.Width(width + 34), GUILayout.Height(20))) MainGUI.SetSelectedAsset(path);
        }

        private void DrawMaterialDictionary(string path) {
            ModelAssetDatabase.FolderData data = folderMap[path];
            bool hasMaterials = data.materials.Count > 0;
            if (hasMaterials || (data.subfolders.Count > 0 && PerformAssetSearch(path, false))) {
                folderMap[path].foldout = DrawConditionalFoldout(path, data, hasMaterials);
            } EditorGUI.indentLevel++;

            if (folderMap[path].foldout) {
                foreach (string subfolder in folderMap[path].subfolders) {
                    DrawMaterialDictionary(subfolder);
                    EditorGUI.indentLevel--;
                } foreach (string materialPath in folderMap[path].materials) DrawMaterialButton(materialPath);
            }
        }

        private void DrawMaterialButton(string path) {
            bool selected = path == SelectedMaterialPath;
            GUIStyle buttonStyle = selected ? UIStyles.HButtonSelected : UIStyles.HButton;
            string pathName = path.IsolatePathEnd("\\/").RemovePathEnd(".");
            float width = EditorUtils.MeasureTextWidth(pathName, GUI.skin.font);
            GUIContent materialContent = new GUIContent(pathName, 
                                                        EditorUtils.FetchIcon(selected ? "d_Material Icon" : "d_Material On Icon"));
            if (GUILayout.Button(materialContent, buttonStyle, GUILayout.Width(width + 29), GUILayout.Height(20))) MainGUI.SetSelectedAsset(path);
        }
    }
}