using System.Collections.Generic;
using UnityEngine;

namespace ModelAssetDatabase {

    /// <summary>
    /// A base class for all database tool tabs;
    /// </summary>
    public abstract class ToolTab : ScriptableObject {

        /// <summary> Every tab will be managed by a parent tool, and will have a handy reference to it; </summary>
        protected BaseTool Tool;

        protected const string INVALID_MANAGER = "You attempted to create a new tab without a proper manager to handle it! The tab was not instantiated;";

        /// <summary>
        /// Initialize base tab data when constructing the tab;
        /// </summary>
        public static T CreateTab<T>(BaseTool tool) where T : ToolTab {
            var tab = CreateInstance<T>();
            tab.Tool = tool;
            tab.InitializeData();
            return tab;
        }

        /// <summary>
        /// Override this method to implement a custom initialization when the tab is created;
        /// </summary>
        protected abstract void InitializeData();

        /// <summary>
        /// Load the corresponding data, if any, when selecting the tab;
        /// </summary>
        public virtual void LoadData(string path) { }

        /// <summary>
        /// Reset tab dependent data when abandoning the tab;
        /// </summary>
        public virtual void ResetData() { }

        void OnDisable() => ResetData();

        public abstract void ShowGUI();
    }

    public abstract class ReaderTab : ToolTab {

        protected Reader Reader;

        protected override void InitializeData() {
            if (Tool is Reader) {
                Reader = Tool as Reader;
            } else Debug.LogError(INVALID_MANAGER);
        }
    }

    public abstract class HierarchyTab : ToolTab {

        protected HierarchyBuilder HierarchyBuilder;

        protected override void InitializeData() {
            if (Tool is HierarchyBuilder) {
                HierarchyBuilder = Tool as HierarchyBuilder;
            } else Debug.LogError(INVALID_MANAGER);
        }
    }

    public class HierarchyTabModels : HierarchyTab {

        /// <summary> Sorted list of all identified models for the search function; </summary>
        private List<string> modelList;
    }

    public class HierarchyTabFolders : HierarchyTab {

        /// <summary> Sorted list of all identified model-containing folders for the search function; </summary>
        private List<string> folderList;
    }

    public class HierarchyTabMaterials : HierarchyTab {

        /// <summary> Sorted list of all identfied materials for the search function; </summary>
        private List<string> materialList;
    }
}