using UnityEngine;
using UnityEditor;
using CJUtils;

public class ModelAssetLibraryImportPreprocessorWindow : EditorWindow {

    [MenuItem("Assets/Library Reimport", false, 50)]
    public static void LibraryReimport() {
        options = new ImportOverrideOptions();
        string path = AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs[0]);
        options.model = AssetImporter.GetAtPath(path) as ModelImporter;
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        options.hasMeshes = mesh != null;
        options.useMaterials = options.hasMeshes ? mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Color) : false;
        options.category = "None";
        ShowWindow();
    }

    [MenuItem("Assets/Library Reimport", true)]
    private static bool LibraryReimportValidate() {
        return Selection.assetGUIDs.Length == 1
               && AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs[0])) is ModelImporter;
    }

    public static ImportOverrideOptions ShowWindow() {
        var window = GetWindow<ModelAssetLibraryImportPreprocessorWindow>();
        window.ShowModal();
        return options;
    }

    private static ImportOverrideOptions options;

    void OnGUI() {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
            EditorUtils.DrawSeparatorLines("Core Import Settings", true);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox)) {
                GUILayout.Label("Asset Type:");
                GUI.enabled = options.hasMeshes;
                GUILayout.Label("Model", GUI.skin.box);
                GUI.enabled = !options.hasMeshes;
                GUILayout.Label("Animation(s)", GUI.skin.box);
                GUI.enabled = true;
            } using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox)) {
                GUILayout.Label("Category:");
                EditorGUILayout.Popup(0, new string[] { "None", "All" } );
            } using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox)) {
                GUILayout.Label("Texture Mode:");
                GUI.enabled = options.hasMeshes;
                if (GUILayout.Button("Material")) {

                } if (GUILayout.Button("Vertex Color")) {

                }
            }
        } using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
            EditorUtils.DrawSeparatorLines("Additional Import Settings", true);
        }
    }
}

public class ImportOverrideOptions {
    public ModelImporter model;
    public bool hasMeshes;
    public bool useMaterials;
    public string category;
}