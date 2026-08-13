using UnityEditor;
using UnityEngine;

namespace Terresquall {
    [CustomEditor(typeof(UISaveSlotManager))]
    public class UISaveSlotManagerEditor : Editor {

        UISaveSlotManager saveSlots;

        public override void OnInspectorGUI() {

            if (!saveSlots.settings) {
                EditorGUILayout.HelpBox("Your Bench Settings scriptable object is not assigned to this component. Please click the button below to create and assign the settings file.", MessageType.Error);
                if (GUILayout.Button("BUTTON NOT CODED YET")) {

                }
            }

            // Render all attributes.
            SerializedProperty property = serializedObject.GetIterator();
            bool enterChildren = true;

            serializedObject.Update();
            while (property.NextVisible(enterChildren)) {
                enterChildren = false;
                switch (property.name) {
                    case "m_Script": break;
                    default:
                        EditorGUI.BeginChangeCheck();
                        EditorGUILayout.PropertyField(property, true);
                        if (EditorGUI.EndChangeCheck()) {
                            serializedObject.ApplyModifiedProperties();
                        }
                        break;
                }
            }

            // Button for generating save slot elements.
            if (GUILayout.Button("Generate Save Slot Elements")) {
                GenerateSaveSlotElements();
            }

        }

        void GenerateSaveSlotElements(bool recordHistory = true) {
            if (!saveSlots.template) return;
            if (!saveSlots.settings) return;

            if(recordHistory)
                Undo.RecordObject(saveSlots, "Updating UI Save Slot object.");

            // Delete all the old generated template objects.
            for (int i = saveSlots.template.transform.parent.childCount - 1; i >= 0; i--) {
                // Get the next child.
                if (i >= saveSlots.template.transform.parent.childCount) continue;
                Transform c = saveSlots.template.transform.parent.GetChild(i);

                // Determines if we should delete this existing object.
                if (c == saveSlots.template.transform) continue;
                if (c.name.Contains(saveSlots.template.name)) {
                    Undo.DestroyObjectImmediate(c.gameObject);
                }
            }

            // Create new template objects.
            int maxSlotNum = saveSlots.settings ? saveSlots.settings.maxSaveSlots : 1;
            saveSlots.slots = new UISaveSlot[maxSlotNum];
            for (int i = 0; i < saveSlots.slots.Length; i++) {
                if (i == 0) saveSlots.slots[0] = saveSlots.template;
                else {
                    saveSlots.slots[i] = Instantiate(saveSlots.template, saveSlots.template.transform.parent);
                    if(recordHistory)
                        Undo.RegisterCreatedObjectUndo(saveSlots.slots[i], "Created new save slot UI.");
                }
            }

            saveSlots.UpdateSaveSlots();

            EditorUtility.SetDirty(saveSlots);
        }

        // Start is called before the first frame update
        void OnEnable() {
            saveSlots = target as UISaveSlotManager;
        }
    }
}