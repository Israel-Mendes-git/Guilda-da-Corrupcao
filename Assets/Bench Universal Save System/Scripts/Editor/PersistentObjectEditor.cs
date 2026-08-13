using System.Reflection;
using UnityEngine;
using UnityEditor;

namespace Terresquall {
    [CustomEditor(typeof(PersistentObject), true)]
    public class PersistentObjectEditor : Editor {

        protected PersistentObject persistentObject;
        protected bool savedDataFoldout = false;
        protected int currentSaveSlot = 0;



        protected virtual void OnEnable() {
            persistentObject = target as PersistentObject;
        }

        public override void OnInspectorGUI() {

            serializedObject.Update();

            DrawSaveIDInspector();
            EditorGUILayout.Space();
            DrawPropertiesInspector();
            EditorGUILayout.Space();
            DrawSaveDataInspector();

            serializedObject.ApplyModifiedProperties();
        }

        public virtual void DrawPropertiesInspector() {
            SerializedProperty prop = serializedObject.GetIterator();
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren)) {
                enterChildren = false;
                switch (prop.name) {
                    case "m_Script":
                    case "saveID":
                        break;
                    default:
                        EditorGUILayout.PropertyField(prop, true);
                        break;
                }
            }
        }

        public virtual void DrawSaveIDInspector() {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("saveID"), true);

            bool isNull = string.IsNullOrEmpty(persistentObject.saveID),
                 isDuplicate = IsDuplicateSaveID();

            if (isNull || isDuplicate) {
                if (isNull)
                    EditorGUILayout.HelpBox("This object will not be saved as it does not have a Save ID.", MessageType.Info);
                else if (isDuplicate)
                    EditorGUILayout.HelpBox("This object's Save ID is a duplicate of another object. Its data may not be saved properly.", MessageType.Warning);

                if (GUILayout.Button("Generate New Save ID")) {
                    Undo.RecordObject(persistentObject, "Generate New Save ID");
                    persistentObject.GenerateSaveID();
                    EditorUtility.SetDirty(persistentObject);
                }
            }

            EditorGUILayout.EndVertical();
        }

        public virtual void DrawSaveDataInspector(GUIStyle style = null) {
            if (style == null) style = BenchEditor.GetSlotGUIStyle();

            EditorGUILayout.BeginVertical(style);
            EditorGUILayout.LabelField("Save Data", EditorStyles.boldLabel);
            currentSaveSlot = Mathf.Max(0, EditorGUILayout.IntField("Slot To Read", currentSaveSlot));

            PersistentObject.SaveData savedData = Bench.ReadAndFind(persistentObject, currentSaveSlot);
            if (savedData != null) {
                EditorGUI.indentLevel++;
                savedDataFoldout = EditorGUILayout.Foldout(savedDataFoldout, savedData.GetClassName(), true, BenchEditor.Styles.FoldoutBoldStyle);
                if (savedDataFoldout) {
                    EditorGUI.indentLevel++;
                    string str = savedData.ToString();
                    BenchEditor.RenderSavePropertyString(str.Substring(str.IndexOf('\n') + 1));
                    EditorGUI.indentLevel--;
                }
                EditorGUI.indentLevel--;
            } else {
                EditorGUILayout.LabelField("There is no saved data for this object.", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();
        }

        // Work in progress. Do not use.
        public static void DrawInspectorField(object data, FieldInfo field, bool editable = false, bool prettifyName = true) {
            string name = prettifyName ? ObjectNames.NicifyVariableName(field.Name) : field.Name;

            if (field.FieldType == typeof(int)) {
                int i = EditorGUILayout.IntField(name, (int)field.GetValue(data));
                if (editable) field.SetValue(data, i);
            } else if (field.FieldType == typeof(float)) {
                float f = EditorGUILayout.FloatField(name, (float)field.GetValue(data));
                if (editable) field.SetValue(data, f);
            } else {
                Object obj = EditorGUILayout.ObjectField(name, field.GetValue(data) as Object, field.FieldType, true);
                if (editable) field.SetValue(data, obj);
            }
        }

        protected virtual bool IsDuplicateSaveID() {
            PersistentObject[] all = Bench.FindObjects<PersistentObject>();
            foreach (PersistentObject p in all)
                if (p != persistentObject && p.saveID == persistentObject.saveID) return true;
            return false;
        }
    }
}