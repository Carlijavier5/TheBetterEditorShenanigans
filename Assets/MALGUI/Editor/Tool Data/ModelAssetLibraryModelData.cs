using UnityEngine;

public class ModelAssetLibraryModelData : ScriptableObject {
    /// <summary> Version number of the Model Data asset, to check for deprecated files; </summary>
    public int version;
    /// <summary> Whether the file was imported as a Model or as an Animation-only file; </summary>
    public bool isModel;
    /// <summary> Whether the model uses materials or vertex color; </summary>
    public bool useMaterials;
    /// <summary> Personalized user notes on the file; </summary>
    public string notes;
}
