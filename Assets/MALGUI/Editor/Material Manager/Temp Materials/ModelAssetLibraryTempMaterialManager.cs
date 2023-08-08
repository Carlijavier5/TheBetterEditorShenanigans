using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class ModelAssetLibraryTempMaterialManager {

    private static string tempPath;
    public static string TempMaterialPath { 
        get {
            if (tempPath == null) {
                string[] guids = AssetDatabase.FindAssets($"t:Script {nameof(ModelAssetLibraryTempMaterialManager)}");
                tempPath = AssetDatabase.GUIDToAssetPath(guids[0]).RemovePathEnd("\\/");
            } return tempPath;
        }
    }

    /// <summary> Maps the GUID of a created material to its asset path; </summary>
    private static Dictionary<Material, string> tempMaterialDict;

    public static void CreateTemporaryMaterialAsset(Material material) {
        if (tempMaterialDict == null) tempMaterialDict = new Dictionary<Material, string>();
        string path = TempMaterialPath + "/" + material.name + ".mat";
        AssetDatabase.CreateAsset(material, path);
        tempMaterialDict[material] = path;
    }

    public static void CleanMaterial(Material material) {
        if (tempMaterialDict.ContainsKey(material) && File.Exists(tempMaterialDict[material])) {
            AssetDatabase.DeleteAsset(tempMaterialDict[material]);
            tempMaterialDict.Remove(material);
        }
    }

    public static void CleanAllMaterials() {
        if (tempMaterialDict == null) return;
        foreach (KeyValuePair<Material, string> kvp in tempMaterialDict) {
            if (File.Exists(kvp.Value)) AssetDatabase.DeleteAsset(kvp.Value);
        } tempMaterialDict = null;
    }
}
