using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class ModelAssetLibraryExtManager {

    /// <summary> Internal reference to the data path, to avoid having to get it so often; </summary>
    private static string dap;
    /// <summary> Path to the folder where external model data will be stored; </summary>
    public static string DataAssetPath {
        get {
            if (dap == null) {
                var assetGUID = AssetDatabase.FindAssets($"t:Script {nameof(ModelAssetLibraryExtData)}");
                dap = AssetDatabase.GUIDToAssetPath(assetGUID[0]).RemovePathEnd("\\/");
            } return dap;
        }
    }

    private readonly static int extVersion = 1; 

    private static Dictionary<string, ModelAssetLibraryExtData> extDataDict;

    public static void Refresh() {
        extDataDict = new Dictionary<string, ModelAssetLibraryExtData>();
        string[] extPaths = ModelAssetLibrary.FindAssets(DataAssetPath, new string[] { "ASSET" });
        foreach (string path in extPaths) {
            var extData = AssetDatabase.LoadAssetAtPath<ModelAssetLibraryExtData>(path);
            if (extData == null) continue;
            if (string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(extData.guid))) MarkExtData(extData);
            else extDataDict[extData.guid] = extData;
        }
    }

    public static void CreateExtData(string modelID) {
        if (extDataDict.ContainsKey(modelID)) return;
        var newExtData = ScriptableObject.CreateInstance<ModelAssetLibraryExtData>();
        newExtData.version = extVersion;
        newExtData.guid = modelID;
        AssetDatabase.CreateAsset(newExtData, DataAssetPath + "/" + modelID + ".asset");
    }

    public static ModelAssetLibraryExtData FetchExtData(string modelID) {
        if (extDataDict.ContainsKey(modelID)) return extDataDict[modelID];
        else return null;
    }

    public static void DeleteExtData(string modelID) {
        if (!extDataDict.ContainsKey(modelID)) return;
        string extDataPath = AssetDatabase.GetAssetPath(extDataDict[modelID]);
        if (!string.IsNullOrEmpty(extDataPath)) AssetDatabase.MoveAssetToTrash(extDataPath);
        extDataDict.Remove(modelID);
    }

    public static void MarkExtData(ModelAssetLibraryExtData extData) {

    }
}