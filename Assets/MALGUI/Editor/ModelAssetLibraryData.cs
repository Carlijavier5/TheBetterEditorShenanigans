using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using PseudoDataStructures;
using CJUtils;

[CreateAssetMenu(fileName=nameof(ModelAssetLibraryData), menuName = "Model Asset Library/Data")]
public class ModelAssetLibraryData : ScriptableObject {
    /*
    /// <summary> Serializable version of the model data dictionary; </summary>
    public StringModelDataDictionary modelDataDict = new StringModelDataDictionary();

    /// <summary> Serializable version of the prefab data dictionary; </summary>
    public StringPrefabDataDictionary prefabDataDict = new StringPrefabDataDictionary();
    */
}

[CustomEditor(typeof(ModelAssetLibraryData))]
public class ModelAssetLibraryDataEditor : Editor {
    /*
    Vector2 scrollPosition;
    
    public override void OnInspectorGUI() {
        ModelAssetLibraryData scripObj = (ModelAssetLibraryData) target;
        using (var scope = new EditorGUILayout.ScrollViewScope(scrollPosition)) {
            scrollPosition = scope.scrollPosition;

            EditorUtils.DrawSeparatorLines("Model Data Dictionary");
            BuildMDDictionary(scripObj.modelDataDict);

            EditorUtils.DrawSeparatorLines("Prefab Data Dictionary");
            BuildPDDictionary(scripObj.prefabDataDict);

            EditorUtils.DrawSeparatorLines("Model - Prefab Association");
            BuildM2PDictionary(scripObj.modelDataDict);

        } if (GUILayout.Button("Clear Data")) ClearData(scripObj);
    }

    private void BuildMDDictionary(StringModelDataDictionary dict) {
        if (dict.Keys.Length == 0) EmptyLabel();
        for (int i = 0; i < dict.Keys.Length; i++) {
            using (new EditorGUILayout.HorizontalScope()) {
                EditorGUILayout.LabelField(AssetDatabase.GUIDToAssetPath(dict.Keys[i]).IsolatePathEnd("\\/").RemovePathEnd("."), GUILayout.Width(160));
                EditorGUILayout.LabelField(dict.Values[i].path, GUILayout.MinWidth(EditorUtils.MeasureTextWidth(dict.Values[i].path, GUI.skin.font) + 16));
            }
        }
    }

    private void BuildPDDictionary(StringPrefabDataDictionary dict) {
        if (dict.Keys.Length == 0) EmptyLabel();
        for (int i = 0; i < dict.Keys.Length; i++) {
            using (new EditorGUILayout.HorizontalScope()) {
                EditorGUILayout.LabelField(AssetDatabase.GUIDToAssetPath(dict.Keys[i]).IsolatePathEnd("\\/").RemovePathEnd("."), GUILayout.Width(160));
                EditorGUILayout.LabelField(dict.Values[i].path, GUILayout.MinWidth(EditorUtils.MeasureTextWidth(dict.Values[i].path, GUI.skin.font) + 16));
            }
        }
    }

    private void BuildM2PDictionary(StringModelDataDictionary dict) {
        if (dict.Keys.Length == 0) EmptyLabel();
        for (int i = 0; i < dict.Keys.Length; i++) {
            using (new EditorGUILayout.HorizontalScope()) {
                EditorGUILayout.LabelField(AssetDatabase.GUIDToAssetPath(dict.Keys[i]).IsolatePathEnd("\\/").RemovePathEnd("."), GUILayout.Width(160));
                var listString = "";
                foreach (string str in dict.Values[i].prefabIDList) listString += AssetDatabase.GUIDToAssetPath(dict.Keys[i]).IsolatePathEnd("\\/").RemovePathEnd(".") + " | ";
                if (string.IsNullOrWhiteSpace(listString)) listString = "-|";
                EditorGUILayout.LabelField(listString.RemovePathEnd("|"));
            }
        }
    }

    private void ClearData(ModelAssetLibraryData scripObj) {
        scripObj.modelDataDict = new StringModelDataDictionary();
        scripObj.prefabDataDict = new StringPrefabDataDictionary();
    }

    private void EmptyLabel() => EditorGUILayout.LabelField(" - Empty - ", UIStyles.ItalicLabel);*/
}