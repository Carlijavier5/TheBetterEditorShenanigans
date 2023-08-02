using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using CJUtils;
using static ModelAssetLibrary;

public class ModelAssetLibraryConfigurationGUI : EditorWindow {

    [MenuItem("Tools/Model Asset Library Config")]
    public static void ShowWindow() {
        ConfigGUI = GetWindow<ModelAssetLibraryConfigurationGUI>("Configuration", typeof(ModelAssetLibraryGUI));
        if (HasOpenInstances<ModelAssetLibraryGUI>()) {
            ModelAssetLibraryGUI.MainGUI.Close();
        }
    }
    
    /// <summary> Reference to the Configuration Window; </summary>
    public static ModelAssetLibraryConfigurationGUI ConfigGUI { get; private set; }

    /// <summary> Path to the Configuration JSON File; </summary>
    private static string ConfigPath {
        get {
            var assetGUID = AssetDatabase.FindAssets($"t:Script {nameof(ModelAssetLibraryConfigurationGUI)}");
            return AssetDatabase.GUIDToAssetPath(assetGUID[0]).RemovePathEnd("\\/") + "/Config.json";
        }
    }

    /// <summary> Collection of assets used by the tool GUI; </summary>
    public static ModelAssetLibraryAssets ToolAssets {
        get {
            var assetGUID = AssetDatabase.FindAssets($"t:ModelAssetLibraryAssets {nameof(ModelAssetLibraryAssets)}");
            return AssetDatabase.LoadAssetAtPath<ModelAssetLibraryAssets>(AssetDatabase.GUIDToAssetPath(assetGUID[0]));
        }
    }

    /// <summary> Temporary string displayed in the text field; </summary>
    public static string potentialPath;

    public struct Configuration {
        public string rootAssetPath;
        public string dictionaryDataPath;
        public string modelFileExtension;
    } public static Configuration Config;

    /// <summary> Path to the root of the folder hierarchy where the library will search for assets; </summary>
    public static string RootAssetPath { get { return Config.rootAssetPath; } }

    /// <summary>
    /// File extension of the assets to look for (without the dot);
    /// </summary>
    public static string ModelFileExtensions { get { return Config.modelFileExtension; } }

    private static Vector2 scrollPosition;

    void OnEnable() {
        LoadConfig();
    }

    private void OnFocus() {
        if (HasOpenInstances<ModelAssetLibraryConfigurationGUI>()
            && ConfigGUI == null) ConfigGUI = GetWindow<ModelAssetLibraryConfigurationGUI>();
    }

    void OnGUI() {
        using (new EditorGUILayout.HorizontalScope(GUI.skin.box)) {
            bool pathIsInvalid = false;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.MaxHeight(100))) {
                EditorUtils.DrawSeparatorLines("Library Settings", true);
                GUILayout.FlexibleSpace();
                using (new EditorGUILayout.HorizontalScope()) {
                    potentialPath = EditorGUILayout.TextField("Root Asset Path", potentialPath);
                    if (AssetDatabase.IsValidFolder(potentialPath) && !potentialPath.EndsWith("/")) {
                        if (potentialPath != Config.rootAssetPath) UpdateRootAssetPath(potentialPath);
                    } else pathIsInvalid = true;
                    if (GUILayout.Button(new GUIContent(EditorUtils.FetchIcon("d_Folder Icon")), GUILayout.MaxWidth(40), GUILayout.MaxHeight(18 ))) {
                        
                        string res = EditorUtility.OpenFolderPanel("Set Root Path", "Assets", "");
                        if (res != null && res.StartsWith(Application.dataPath)) {
                            res = "Assets" + res.Substring(Application.dataPath.Length);
                            UpdateRootAssetPath(res);
                        }
                    }
                } GUILayout.FlexibleSpace();
                Config.modelFileExtension = EditorGUILayout.TextField("Model File Extension(s)", Config.modelFileExtension);
                GUILayout.FlexibleSpace();
                if (pathIsInvalid) GUI.enabled = false;
                if (GUILayout.Button("Save Changes")) SaveConfig();
                if (pathIsInvalid) GUI.enabled = true;
            } using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.MaxHeight(100))) {
                EditorUtils.DrawSeparatorLines("Data & Documentation", true);
                GUILayout.FlexibleSpace();
                if (pathIsInvalid) GUI.enabled = false;
                if (GUILayout.Button("Open Asset Library")) {
                    ModelAssetLibraryGUI.ShowWindow(); 
                } if (GUILayout.Button("Reload Asset Library")) {
                    Refresh();
                } if (pathIsInvalid) GUI.enabled = true;
                if (GUILayout.Button("Open Documentation")) {
                    Debug.Log("There's no documentation to show here... YET! >:)");
                }
            }
        } using (new EditorGUILayout.VerticalScope(new GUIStyle(GUI.skin.box) { padding = new RectOffset(20, 20, 0, 0)})) {
            EditorUtils.DrawSeparatorLines("Library Data", true);
            using (var view = new EditorGUILayout.ScrollViewScope(scrollPosition)) {
                scrollPosition = view.scrollPosition;
                EditorUtils.DrawSeparatorLines("Model Data Dictionary");
                BuildMDDictionary(ModelDataDict);

                EditorUtils.DrawSeparatorLines("Prefab Data Dictionary");
                BuildPDDictionary(PrefabDataDict);

                EditorUtils.DrawSeparatorLines("Model - Prefab Association");
                BuildM2PDictionary(ModelDataDict);
            }
        }
    }

    /// <summary>
    /// Replace the Root Asset Path statically. The path still needs to be saved;
    /// </summary>
    /// <param name="newAssetPath"></param>
    private static void UpdateRootAssetPath(string newAssetPath) {
        Config.rootAssetPath = newAssetPath.Trim('/');
        Refresh();
        potentialPath = newAssetPath;
    }

    /// <summary>
    /// Save configuration data as a JSON string on this script's folder;
    /// </summary>
    public static void SaveConfig() {
        string data = JsonUtility.ToJson(Config);
        using StreamWriter writer = new StreamWriter(ConfigPath);
        writer.Write(data);
    }

    /// <summary>
    /// Load configuration data from a JSON string located in this script's folder;
    /// </summary>
    public static void LoadConfig() {
        if (File.Exists(ConfigPath)) {
            using StreamReader reader = new StreamReader(ConfigPath);
            string data = reader.ReadToEnd();
            Config = JsonUtility.FromJson<Configuration>(data);
            potentialPath = Config.rootAssetPath;
        } else {
            Config = new Configuration();
        }
    }

    /// <summary>
    /// Displays data on the Model Data Dictionary;
    /// </summary>
    /// <param name="dict"> Model Data Dictionary; </param>
    private void BuildMDDictionary(Dictionary<string, ModelData> dict) {
        if (dict == null || dict.Keys.Count == 0) {
            EmptyLabel();
            return;
        } foreach (KeyValuePair<string, ModelData> kvp in dict) {
            using (new EditorGUILayout.HorizontalScope()) {
                EditorGUILayout.LabelField(AssetDatabase.GUIDToAssetPath(kvp.Key).IsolatePathEnd("\\/").RemovePathEnd("."), GUILayout.Width(160));
                EditorGUILayout.LabelField(kvp.Value.path, GUILayout.MinWidth(EditorUtils.MeasureTextWidth(kvp.Value.path, GUI.skin.font) + 16));
            }
        }
    }

    /// <summary>
    /// Displays data on the Prefab Data Dictionary;
    /// </summary>
    /// <param name="dict"> Prefab Data Dictionary; </param>
    private void BuildPDDictionary(Dictionary<string, PrefabData> dict) {
        if (dict == null || dict.Keys.Count == 0) {
            EmptyLabel();
            return;
        } foreach (KeyValuePair<string, PrefabData> kvp in dict) {
            using (new EditorGUILayout.HorizontalScope()) {
                EditorGUILayout.LabelField(AssetDatabase.GUIDToAssetPath(kvp.Key).IsolatePathEnd("\\/").RemovePathEnd("."), GUILayout.Width(160));
                EditorGUILayout.LabelField(kvp.Value.path, GUILayout.MinWidth(EditorUtils.MeasureTextWidth(kvp.Value.path, GUI.skin.font) + 16));
            }
        }
    }

    /// <summary>
    /// Displays the prefab correlation lists in the Model Data Dictionary;
    /// </summary>
    /// <param name="dict"> Model Data Dictionary; </param>
    private void BuildM2PDictionary(Dictionary<string, ModelData> dict) {
        if (dict == null || dict.Keys.Count == 0) {
            EmptyLabel();
            return;
        } foreach (KeyValuePair<string, ModelData> kvp in dict) {
            using (new EditorGUILayout.HorizontalScope()) {
                EditorGUILayout.LabelField(AssetDatabase.GUIDToAssetPath(kvp.Key).IsolatePathEnd("\\/").RemovePathEnd("."), GUILayout.Width(160));
                var listString = "";
                foreach (string str in kvp.Value.prefabIDList) {
                    listString += AssetDatabase.GUIDToAssetPath(str).IsolatePathEnd("\\/").RemovePathEnd(".") + " | ";
                } if (string.IsNullOrWhiteSpace(listString)) listString = "-|";
                EditorGUILayout.LabelField(listString.RemovePathEnd("|"));
            }
        }
    }

    private void EmptyLabel() => EditorGUILayout.LabelField(" - Empty - ", UIStyles.ItalicLabel);
}