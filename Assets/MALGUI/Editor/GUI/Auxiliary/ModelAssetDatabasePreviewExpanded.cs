using UnityEngine;
using UnityEditor;
using CJUtils;
using MADUtils;

/// <summary>
/// Simple window to show a window-sized object preview;
/// </summary>
public class ModelAssetDatabasePreviewExpanded : EditorWindow {

    /// <summary>
    /// Show a separate Window with a fully expanded preview of a given GameObject;
    /// <br></br> Note that this is a shared Object Preview from the Asset Library Reader;
    /// </summary>
    /// <param name="gameObject"> GameObject to preview; </param>
    public static void ShowPreviewWindow(GameObject gameObject) {
        var window = GetWindow<ModelAssetDatabasePreviewExpanded>("Expanded Preview");
        window.previewObject = gameObject;
    }

    /// <summary> GameObject to show in the preview; </summary>
    private GameObject previewObject;
    private GenericPreview preview;

    void OnGUI() {
        if (previewObject == null) {
            EditorUtils.DrawScopeCenteredText("Oh, Great Lady of Assembly Reloads...\nShow us your wisdom! And reload this page...");
        } else {
            if (preview is null) preview = new GenericPreview(previewObject);
            preview.DrawPreview(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        }
    }

    void OnDisable() {
        if (preview != null) preview.CleanUp(ref preview);
    }
}