using UnityEngine;

public abstract class ModelAssetDatabaseTool : Object {

    /// Required Tool Core;

    /// <summary>
    /// Override this method to Initialize the tool when created;
    /// </summary>
    protected abstract void InitializeData();
    /// <summary>
    /// Override this method to refresh tool values when the tool is reselected;
    /// </summary>
    public virtual void RefreshData() { }
    /// <summary>
    /// Override this method to reset tool values when another tool is selected;
    /// </summary>
    public virtual void ResetData() { }
    /// <summary>
    /// Override this method to dispose of unmanaged tool data before the tool instance is disposed of;
    /// </summary>
    public abstract void FlushData();
    /// <summary>
    /// Override this method to change the selected asset on a Hierarchy Builder call;
    /// </summary>
    /// <param name="path"> Path of the asset to select; </param>
    public virtual void SetSelectedAsset(string path) { }

    public ModelAssetDatabaseTool() => InitializeData();

    /// Required Tool GUI;

    /// <summary>
    /// Override this method to implement the tool's toolbar; 
    /// </summary>
    public abstract void DrawToolbar();
    /// <summary>
    /// Override this method to implement the tool's GUI; 
    /// </summary>
    public abstract void ShowGUI();
}