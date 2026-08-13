using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

namespace Terresquall {
    [CustomEditor(typeof(Bench), true)]
    public class BenchEditor : Editor {

        Bench bench;

        public class Styles {

            public struct Colors {
                public static Color red = new Color(1f, .8f, .8f);
            }

            static GUIStyle centeredMiniWordWrappedLabel;
            public static GUIStyle CenteredMiniWordWrappedLabel {
                get { 
                    if(centeredMiniWordWrappedLabel == null) {
                        centeredMiniWordWrappedLabel = new GUIStyle(EditorStyles.wordWrappedMiniLabel);
                        centeredMiniWordWrappedLabel.alignment = TextAnchor.MiddleCenter;
                    }
                    return centeredMiniWordWrappedLabel;
                }
            }

            static GUIStyle foldoutBoldStyle;
            public static GUIStyle FoldoutBoldStyle {
                get {
                    if(foldoutBoldStyle != null) return foldoutBoldStyle;
                    GUIStyle g = new GUIStyle(EditorStyles.foldout);
                    g.fontStyle = FontStyle.Bold;
                    return g;
                }
            }
        }

        class SaveStringData {
            public string output;
            public bool isOpen, isEmpty, isCorrupt = false;
        }
        Dictionary<int, SaveStringData> data = new Dictionary<int, SaveStringData>();
        bool currentSaveFoldout = false;

        void OnEnable() {
            bench = target as Bench;
        }

        public static Bench FindSettings() {
            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject");
            foreach (string s in guids) {
                // Load the first found settings asset
                string path = AssetDatabase.GUIDToAssetPath(s);
                ScriptableObject data = AssetDatabase.LoadAssetAtPath<Bench>(path);
                if (data is Bench) return data as Bench;
            }
            return null;
        }

        public virtual void DrawMultipleSettingsFileChecker() {
            string[] guids = AssetDatabase.FindAssets("t:Bench");

            if (guids.Length > 1) {
                Color origColor = GUI.backgroundColor;
                GUI.backgroundColor = Styles.Colors.red;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label(
                    EditorGUIUtility.IconContent("console.erroricon"),
                    GUILayout.Width(40),
                    GUILayout.Height(40)
                );
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                EditorGUILayout.Space(-10);

                // Warning message.
                EditorGUILayout.LabelField(
                    "Multiple Bench Settings detected. Please remove all duplicates.",
                    Styles.CenteredMiniWordWrappedLabel, GUILayout.Height(30)
                );
                if (GUILayout.Button("Delete Duplicate Settings")) {
                    string path = AssetDatabase.GetAssetPath(target);
                    try {
                        AssetDatabase.DeleteAsset(path);
                        Debug.Log($"Successfully deleted duplicated asset at {path}.");
                        return;
                    } catch(UnityException e) {
                        Debug.LogWarning($"Failed to delete asset at {path}: {e.Message}");
                    }
                }
                EditorGUILayout.EndVertical();
                GUI.backgroundColor = origColor;
                EditorGUILayout.Space();
            }
        }

        public override void OnInspectorGUI() {

            // Draw the settings checker.
            DrawMultipleSettingsFileChecker();

            // Create serialized object if null or target changed
            if (!target) return;
            SerializedProperty iterator = serializedObject.GetIterator();
            serializedObject.Update();

            if (iterator.NextVisible(true)) {
                do {
                    switch (iterator.name) {
                        case "m_Script": continue;
                        default:
                            EditorGUILayout.PropertyField(iterator, true);
                            break;
                    }
                } while (iterator.NextVisible(false));
            }

            if(EditorApplication.isPlaying) {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.TextField("Current Save Slot", Bench.currentSlot.ToString());
                EditorGUILayout.TextField("Last Playtime", Bench.lastPlaytime.ToString());

                currentSaveFoldout = EditorGUILayout.Foldout(currentSaveFoldout, "Current Save File", true);
                if (currentSaveFoldout) {
                    EditorGUILayout.BeginVertical(GetSlotGUIStyle());
                    EditorGUI.indentLevel++;
                    Bench.SaveFile sav = Bench.GetCurrentSaveFile();
                    if (sav == null) EditorGUILayout.LabelField("No save data.");
                    else RenderSavePropertyString(Bench.GetCurrentSaveFile().ToString());
                    EditorGUI.indentLevel--;
                    EditorGUILayout.EndVertical();
                }

                EditorGUI.EndDisabledGroup();
            }

            // Show UI for examining existing saves in slots.
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Editor Saves", EditorStyles.boldLabel);
            for (int i = 0; i < bench.maxSaveSlots; i++) {

                // Backward compatibility with older versions of Unity.
                EditorGUILayout.BeginVertical(GetSlotGUIStyle());

                // If data is already loaded, we show the data.
                if (data.ContainsKey(i)) {
                    // If the data is empty, we will just print the data without any foldout.
                    if (data[i].isEmpty) {
                        EditorGUILayout.LabelField(data[i].output, Styles.CenteredMiniWordWrappedLabel);
                        if(data[i].isCorrupt) {
                            Color orig = GUI.backgroundColor;
                            GUI.backgroundColor = Styles.Colors.red;
                            if(GUILayout.Button($"Delete Data in Slot {i}")) {
                                Bench.Delete(i);
                                data.Remove(i);
                            }
                            GUI.backgroundColor = orig;
                        }
                    } else {
                        EditorGUI.indentLevel++;
                        data[i].isOpen = EditorGUILayout.Foldout(data[i].isOpen, $"Slot {i}", true);
                        if (data[i].isOpen) {
                            EditorGUI.indentLevel++;
                            RenderSavePropertyString(data[i].output);

                            Color orig = GUI.backgroundColor;
                            GUI.backgroundColor = new Color(1f, .8f, .8f);
                            if(GUILayout.Button($"Delete Data in Slot {i}")) {
                                Bench.Delete(i);
                                data.Remove(i);
                            }
                            GUI.backgroundColor = orig;
                            EditorGUI.indentLevel--;
                        }
                        EditorGUI.indentLevel--;
                    }
                } else {

                    if (Bench.SlotHasSave(i)) {
                        // Otherwise display the button for loading the data in the slot.
                        if (GUILayout.Button($"View Saved Data in Slot {i}")) {
                            // Attempts to load the game in the slot.
                            Bench.SaveFile save = Bench.LoadGame(i);
                            if (save == null) {
                                data[i] = new SaveStringData { output = $"No data in Slot {i}", isEmpty = true };
                                EditorGUILayout.EndVertical();
                                break;
                            } else if(save.exception != null) {
                                data[i] = new SaveStringData { output = $"Save data is corrupt or outdated. Check the Console for more information.", isEmpty = true, isCorrupt = true };
                                Debug.LogWarning($"Save data is corrupt or outdated:\n\n{save.exception.Message}");
                                EditorGUILayout.EndVertical();
                                break;
                            }

                            data[i] = new SaveStringData { output = save.ToString(), isOpen = true };
                        }
                    } else {
                        EditorGUILayout.LabelField($"No data in Slot {i}.", EditorStyles.centeredGreyMiniLabel);
                    }
                }

                EditorGUILayout.EndVertical();
            }

            // Button to open save folder.
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Shortcuts", EditorStyles.boldLabel);
            if (GUILayout.Button("Open Save Path")) OpenPath(Bench.GetSavePath());
            if (GUILayout.Button("Delete Editor Saves")) ClearSaves();

            serializedObject.ApplyModifiedProperties();
        }

        public static GUIStyle GetSlotGUIStyle() {
            // Backward compatibility with older versions of Unity.
            GUIStyle selectionRect = GUI.skin.FindStyle("selectionRect");
            return selectionRect == null ? EditorStyles.helpBox : selectionRect;
        }

        public static void RenderSavePropertyString(string input) {
            StringReader reader = new StringReader(input);
            string line;
            Color oldColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(.8f, .8f, .8f, 1f);
            while ((line = reader.ReadLine()) != null) {
                string[] split = line.Split(new char[] { ':' }, 2);
                if (split.Length > 1) {
                    char[] trims = { '-', ' ' };
                    EditorGUILayout.DelayedTextField(
                        ObjectNames.NicifyVariableName(split[0].Trim(trims)), 
                        split[1].Trim(trims)
                    );
                } else if (line.Trim().Length <= 0) {
                    EditorGUILayout.Space();
                } else {
                    EditorGUILayout.LabelField(line, EditorStyles.boldLabel);
                }
            }
            EditorGUILayout.Space();
            GUI.backgroundColor = oldColor;
        }

        void OpenPath(string subfolder) {
            string path = Bench.GetSavePath();

            // Create folder if it doesn't exist
            if (!Directory.Exists(path)) {
                Directory.CreateDirectory(path);
            }

            // Open the folder in Explorer/Finder
#if UNITY_EDITOR_WIN
            System.Diagnostics.Process.Start("explorer.exe", path.Replace("/", "\\"));
#elif UNITY_EDITOR_OSX
            System.Diagnostics.Process.Start("open", path);
#elif UNITY_EDITOR_LINUX
            System.Diagnostics.Process.Start("xdg-open", path);
#endif
        }

        void ClearSaves() {
            string path = Bench.GetSavePath();
            if (Directory.Exists(path)) {
                Directory.Delete(path, true);
                Debug.Log("Successfully deleted existing saves.");
            } else {
                Debug.Log("No save files exist.");
            }
        }
    }
}
