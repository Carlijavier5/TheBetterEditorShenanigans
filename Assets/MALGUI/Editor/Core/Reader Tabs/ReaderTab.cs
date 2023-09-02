using UnityEngine;

namespace ModelAssetDatabase {
    public abstract class ReaderTab : BaseTab {

        protected Reader Reader;

        protected override void InitializeData() {
            if (Tool is Reader) {
                Reader = Tool as Reader;
            } else Debug.LogError(INVALID_MANAGER);
        }
    }
}