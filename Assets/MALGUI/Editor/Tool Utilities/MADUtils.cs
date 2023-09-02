using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace ModelAssetDatabase {

    namespace MADUtils {

        /// <summary>
        /// Utility class for ease of use and disposal of object previews;
        /// </summary>
        public class GenericPreview : ScriptableObject {

            /// <summary>
            /// Creates an object preview for the given Object;
            /// <br></br> The preview will persist unscathed even if the object is destroyed;
            /// </summary>
            /// <param name="previewObject"> Object to create a preview for; </param>
            public static GenericPreview CreatePreview(Object previewObject) {
                var so = CreateInstance<GenericPreview>();
                so.preview = Editor.CreateEditor(previewObject);
                return so;
            }

            /// <summary> Preview generated by the class; </summary>
            public Editor preview;

            /// <summary>
            /// Draws a preview with the given parameters;
            /// </summary>
            /// <param name="options"> Options to draw the preview with; </param>
            public void DrawPreview(params GUILayoutOption[] options) {
                Rect rect = EditorGUILayout.GetControlRect(options);
                if (preview) preview.DrawPreview(rect);
            }

            /// <summary>
            /// Cleans the preview editor and destroys the outer preview;
            /// </summary>
            public void OnDestroy() {
                DestroyImmediate(preview);
                preview = null;
            }
        }

        /// <summary>
        /// Utility class for ease of use and disposal of Material Editors;
        /// </summary>
        public class MaterialEditorBundle : ScriptableObject {

            public MaterialEditor editor;

            /// <summary>
            /// Creates an object preview for the given Object;
            /// <br></br> The preview will persist unscathed even if the object is destroyed;
            /// </summary>
            /// <param name="material"> Material to create a preview for; </param>
            public static MaterialEditorBundle CreateBundle(Material material) {
                var so = CreateInstance<MaterialEditorBundle>();
                so.editor = Editor.CreateEditor(material, typeof(MaterialEditor)) as MaterialEditor;
                return so;
            }

            public bool DrawEditor() {
                editor.serializedObject.Update();
                var changeDetection = typeof(MaterialEditor)
                                        .GetMethod("DetectShaderEditorNeedsUpdate", BindingFlags.NonPublic
                                        | BindingFlags.Instance);
                changeDetection.Invoke(editor, null);
                MaterialProperty[] properties = MaterialEditor.GetMaterialProperties(new Object[] { editor.target });
                EditorGUI.BeginChangeCheck();
                if (editor.customShaderGUI != null) {
                    editor.customShaderGUI.OnGUI(editor, properties);
                } else editor.PropertiesDefaultGUI(properties);
                bool changeDetected = EditorGUI.EndChangeCheck();
                if (changeDetected) editor.PropertiesChanged();
                return changeDetected;
            }

            void OnDestroy() => DestroyImmediate(editor);
        }

        /// <summary>
        /// General utilities for the model AssetLibrary;
        /// </summary>
        public static class GeneralUtils {

            /// <summary> Potential results for the name validation process; </summary>
            public enum InvalidNameCondition {
                None,
                Empty,
                Overwrite,
                Symbol,
                Convention,
                Success
            }

            /// <summary>
            /// Validate a filename in terms of content, convention, and File I/O availability;
            /// </summary>
            /// <returns> True if the name is valid, false otherwise; </returns>
            public static InvalidNameCondition ValidateFilename(string path, string name) {
                if (string.IsNullOrWhiteSpace(name)) {
                    return InvalidNameCondition.Empty;
                } if (!ModelAssetDatabase.NoAssetAtPath(path)) {
                    return InvalidNameCondition.Overwrite;
                } if (NameViolatesConvention(name)) {
                    return InvalidNameCondition.Convention;
                } List<char> invalidChars = new List<char>(Path.GetInvalidFileNameChars());
                foreach (char character in name) {
                    if (invalidChars.Contains(character)) {
                        return InvalidNameCondition.Symbol;
                    }
                } return InvalidNameCondition.None;
            }

            private static bool NameViolatesConvention(string fileName) {
                if (string.IsNullOrWhiteSpace(fileName)) return true;
                if (!char.IsUpper(fileName[0])) return true;
                if (fileName.Contains(" ")) return true;
                return false;
            }
        }

        public static class MaterialUtils {

            /// <summary>
            /// Fetches a bunch of internal serialized references from the Model Importer;
            /// </summary>
            public static Dictionary<string, Material> LoadInternalMaterialMap(ModelImporter model) {
                Dictionary<string, Material> internalMap = new Dictionary<string, Material>();
                using (SerializedObject serializedObject = new SerializedObject(model)) {
                    SerializedProperty materials = serializedObject.FindProperty("m_Materials");
                    SerializedProperty extObjects = serializedObject.FindProperty("m_ExternalObjects");

                    for (int matIndex = 0; matIndex < materials.arraySize; matIndex++) {
                        SerializedProperty materialID = materials.GetArrayElementAtIndex(matIndex);
                        string name = materialID.FindPropertyRelative("name").stringValue;
                        string type = materialID.FindPropertyRelative("type").stringValue;

                        Object materialRef = null;
                        for (int externalObjectIndex = 0; externalObjectIndex < extObjects.arraySize; externalObjectIndex++) {
                            SerializedProperty extObject = extObjects.GetArrayElementAtIndex(externalObjectIndex);
                            string extName = extObject.FindPropertyRelative("first.name").stringValue;
                            string extType = extObject.FindPropertyRelative("first.type").stringValue;

                            if (extType == type && extName == name) {
                                materialRef = extObject.FindPropertyRelative("second").objectReferenceValue;
                                break;
                            }
                        } internalMap[name] = materialRef as Material;
                    }
                } return internalMap;
            }

            /// <summary>
            /// Replaces a serialized material reference with another in the target model importer;
            /// <br></br> If the passed material is null, deletes the corresponding external map key;
            /// </summary>
            /// <param name="key"> Name of the material binding to change; </param>
            /// <param name="newMaterial"> Material to place in the binding; </param>
            public static void ReplacePersistentMaterial(string key, Material newMaterial, ModelImporter Model) {

                using (SerializedObject serializedObject = new SerializedObject(Model)) {
                    SerializedProperty extObjects = serializedObject.FindProperty("m_ExternalObjects");

                    if (newMaterial != null) {
                        /// Note: I'm aware the process below could be optimized by accounting for the value assigned in the
                        /// StaticMaterialSlots map; however, to ensure the method accounts for external changes in the
                        /// importer map, it's better to operate on the nominal values rather than the tool's own;
                        SerializedProperty materials = serializedObject.FindProperty("m_Materials");
                        for (int matIndex = 0; matIndex < materials.arraySize; matIndex++) {
                            SerializedProperty materialID = materials.GetArrayElementAtIndex(matIndex);
                            string name = materialID.FindPropertyRelative("name").stringValue;
                            string type = materialID.FindPropertyRelative("type").stringValue;

                            if (name == key) {
                                string assembly = materialID.FindPropertyRelative("assembly").stringValue;
                                bool lacksKey = true;
                                for (int externalObjectIndex = 0; externalObjectIndex < extObjects.arraySize; externalObjectIndex++) {
                                    SerializedProperty extObject = extObjects.GetArrayElementAtIndex(externalObjectIndex);
                                    string extName = extObject.FindPropertyRelative("first.name").stringValue;
                                    string extType = extObject.FindPropertyRelative("first.type").stringValue;

                                    if (extType == type && extName == name) {
                                        extObject.FindPropertyRelative("second").objectReferenceValue = newMaterial;
                                        lacksKey = false;
                                        break;
                                    }
                                } if (lacksKey) {
                                    int lastIndex = extObjects.arraySize++;
                                    SerializedProperty newObj = extObjects.GetArrayElementAtIndex(lastIndex);
                                    newObj.FindPropertyRelative("first.name").stringValue = name;
                                    newObj.FindPropertyRelative("first.type").stringValue = type;
                                    newObj.FindPropertyRelative("first.assembly").stringValue = assembly;
                                    newObj.FindPropertyRelative("second").objectReferenceValue = newMaterial;
                                }
                            }
                        }
                    } else {
                        for (int externalObjectIndex = 0; externalObjectIndex < extObjects.arraySize; externalObjectIndex++) {
                            SerializedProperty extObject = extObjects.GetArrayElementAtIndex(externalObjectIndex);
                            string extName = extObject.FindPropertyRelative("first.name").stringValue;
                            if (extName == key) extObjects.DeleteArrayElementAtIndex(externalObjectIndex);
                        }
                    } serializedObject.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        public static class SearchingUtils {
            /// <summary>
            /// Alphanumerical sort comparison expression;
            /// </summary>
            /// <param name="name1"> First string; </param>
            /// <param name="name2"> Second string; </param>
            /// <returns> A comparison integer between two strings based on lexicographical order; </returns>
            public static int AlnumSort(string name1, string name2) => name1.IsolatePathEnd("\\/").CompareTo(name2.IsolatePathEnd("\\/"));
        }
    }
}
