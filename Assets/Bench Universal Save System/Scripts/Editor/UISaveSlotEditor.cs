using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Terresquall {
    [CustomEditor(typeof(UISaveSlot))]
    public class UISaveSlotEditor : Editor {

        public Dictionary<string, string> dateFormats = new Dictionary<string, string>() {
            { "1 Jan 1970", "d MMM yyyy" },
            { "1 Jan 1970, 13:00", "d MMM yyyy, HH:mm" },
            { "1 Jan 1970, 13:00:00", "d MMM yyyy, HH:mm:ss" },
            { "1 Jan 1970, 1:00 PM", "d MMM yyyy, hh:mm tt" },
            { "1 Jan 1970, 1:00:00 PM", "d MMM yyyy, hh:mm:ss tt" }
        };
        public Dictionary<string, string> playtimeFormats = new Dictionary<string, string>() {
            { "1h 30m 40s", @"h'h 'mm'm 'ss's'" },
            { "01:30:45", @"hh':'mm':'ss" }
        };

        int selectedDateFormat = 0, selectedPlaytimeFormat = 0;
        UISaveSlot saveSlot;

        void OnEnable() {
            saveSlot = target as UISaveSlot;
        }

        public override void OnInspectorGUI() {
            serializedObject.Update();

            // Check if there is a SaveSlotManager. If not, show tooltip.
            UISaveSlotManager m = saveSlot.GetComponentInParent<UISaveSlotManager>();
            if (!m.settings) {
                EditorGUILayout.HelpBox("Please configure your Bench settings under Window > Terresquall > Bench Universal Save System to start using the save system.", MessageType.Warning);
            }

            SerializedProperty property = serializedObject.GetIterator();
            bool enterChildren = true;

            while (property.NextVisible(enterChildren)) {
                enterChildren = false;
                switch (property.name) {
                    case "m_Script": break;
                    case "dateFormat":
                        EditorGUI.BeginChangeCheck();
                        selectedDateFormat = EditorGUILayout.Popup("Date Format", selectedDateFormat, dateFormats.Keys.ToArray());
                        if (EditorGUI.EndChangeCheck()) {
                            saveSlot.dateFormat = dateFormats.Values.ToArray()[selectedDateFormat];
                            EditorUtility.SetDirty(saveSlot);
                        }
                        break;
                    case "playtimeFormat":
                        EditorGUI.BeginChangeCheck();
                        selectedPlaytimeFormat = EditorGUILayout.Popup("Playtime Format", selectedPlaytimeFormat, playtimeFormats.Keys.ToArray());
                        if (EditorGUI.EndChangeCheck()) {
                            saveSlot.playtimeFormat = playtimeFormats.Values.ToArray()[selectedPlaytimeFormat];
                            EditorUtility.SetDirty(saveSlot);
                        }
                        break;
                    default:
                        EditorGUI.BeginChangeCheck();
                        EditorGUILayout.PropertyField(property, true);
                        if (EditorGUI.EndChangeCheck()) {
                            serializedObject.ApplyModifiedProperties();
                        }
                        break;
                }
            }

            serializedObject.ApplyModifiedProperties();

            // Update save slots button.
            if (m && m.settings) {
                if (GUILayout.Button("Update Save Slots")) {
                    m.UpdateSaveSlots();
                }
            }
        }
    }
}