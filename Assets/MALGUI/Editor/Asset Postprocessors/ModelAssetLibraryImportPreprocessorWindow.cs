using UnityEngine;
using UnityEditor;

public class ModelAssetLibraryImportPreprocessorWindow : EditorWindow {

    public static ImportOverrideOptions ShowWindow() {
        var window = GetWindow<ModelAssetLibraryImportPreprocessorWindow>();
        window.ShowModal();
        return options;
    }

    private static ImportOverrideOptions options;

    void OnEnable() {
        options = new ImportOverrideOptions();
    }

    void OnGUI() {
        if (GUILayout.Button("Message")) {
            options.printMe = "AYAYAYAYA";
        } if (GUILayout.Button("Import")) {
            Close();
        }
    }
}

public class ImportOverrideOptions {
    public string printMe;
}
