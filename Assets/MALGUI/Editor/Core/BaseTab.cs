using UnityEngine;
using CJUtils;

namespace ModelAssetDatabase {

    /// <summary>
    /// A base class for all database tool tabs;
    /// </summary>
    public abstract class BaseTab : ScriptableObject {

        /// <summary> Every tab will be managed by a parent tool, and will have a handy reference to it; </summary>
        protected BaseTool Tool;

        protected const string INVALID_MANAGER = "You attempted to create a new tab without a proper manager to handle it! The tab was not instantiated;";

        /// <summary>
        /// Initialize base tab data when constructing the tab;
        /// </summary>
        public static T CreateTab<T>(BaseTool tool) where T : BaseTab {
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
        /// Override to load the data corresponding to a path, usually on asset creation;
        /// This method may also be used by the Hierarchy for GUI purposes;
        /// </summary>
        public virtual void LoadData(string path) { }

        /// <summary>
        /// Reset tab dependent data when abandoning the tab;
        /// </summary>
        public virtual void ResetData() { }
        void OnDisable() => ResetData();

        /// <summary>
        /// Override to display Custom GUI controls in the Tab;
        /// </summary>
        public virtual void ShowGUI() => EditorUtils.DrawScopeCenteredText("No GUI has been implemented for this tab;");
    }

    /// <summary>
    /// Base class for all tabs managed by the Material Manager Tool;
    /// </summary>
    public abstract class MaterialTab : BaseTab {

        /// <summary> The Material Manager parent tool of this tab; </summary>
        protected MaterialManager MaterialManager;

        protected override void InitializeData() {
            if (Tool is MaterialManager) {
                MaterialManager = Tool as MaterialManager;
            } else Debug.LogError(INVALID_MANAGER);
        }
    }

    public class MaterialTabEditor : MaterialTab {

    }

    public class MaterialTabCreator : MaterialTab {

    }

    public class MaterialTabOrganizer : MaterialTab {

    }

    public class MaterialTabReplacer : MaterialTab {

    }
}