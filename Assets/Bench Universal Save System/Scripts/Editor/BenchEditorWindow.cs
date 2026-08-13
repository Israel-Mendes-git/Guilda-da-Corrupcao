using UnityEngine;
using UnityEditor;

namespace Terresquall {
    public class BenchEditorWindow : EditorWindow {

        Bench settings;
        public Bench Settings { 
            get {
                if (!settings) {
                    // Prevent memory leaks if there is an existing editor.
                    if (settingsEditor != null) DestroyImmediate(settingsEditor);
                    settings = BenchEditor.FindSettings();
                }
                return settings; 
            } 
        }

        Editor settingsEditor;
        Vector2 scrollPosition;
        static BenchEditorWindow instance;

        [MenuItem("Window/Bench Universal Save System", priority = 100000)]
        public static void ShowWindow() {
            instance = GetWindow<BenchEditorWindow>("Bench Save");
        }

        void OnGUI() {

            if (!Settings) {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("There are no settings found for the Bench Save System. Please create a new set of settings.", MessageType.Warning);
                if (GUILayout.Button("Create New Save Settings")) {
                    try {
                        settings = CreateInstance<Bench>();
                        AssetDatabase.CreateAsset(settings, $"Assets/Bench Settings.asset");
                        AssetDatabase.SaveAssets();
                    } catch(UnityException e) {
                        Debug.LogError("Failed to create Bench settings file. Please create one from the Project window with Right-click > Bench Universal Save System > Settings.\n" + e.Message);
                        settings = null;
                    }
                }

                return;
            }

            if (settingsEditor == null || settingsEditor.target != settings) {
                // Create a cached custom Editor instance
                settingsEditor = Editor.CreateEditor(Settings);
            }

            if (settingsEditor != null) {
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, EditorStyles.inspectorDefaultMargins);
                settingsEditor.OnInspectorGUI();  // Reuse the inspector drawing logic
                EditorGUILayout.EndScrollView();
            }
        }

    }
}