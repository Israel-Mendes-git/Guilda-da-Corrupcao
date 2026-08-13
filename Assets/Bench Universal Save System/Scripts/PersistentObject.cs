using UnityEngine;
using System;
using System.Reflection;
using System.Text;

namespace Terresquall {
    public abstract class PersistentObject : MonoBehaviour {
        public string saveID; // Unique string to identify this object when loading.

        protected virtual void Reset() {
            // Automatically assigns the object ID
            if (string.IsNullOrEmpty(saveID)) {
                GenerateSaveID();
            }
        }

        protected virtual void OnDrawGizmos() {
            Color c = string.IsNullOrEmpty(saveID) ? new Color(1, .7f, .67f) : new Color(.8f, 1, .8f);
            Gizmos.DrawIcon(transform.position, "d_SaveAs", true, c);
        }

        public virtual string GenerateSaveID() {
            saveID = Guid.NewGuid().ToString();
            return saveID;
        }

        // For use by child classes to determine if this object should be saved.
        // Can be overriden to add more criteria, but the default is if the saveID is unset.
        public virtual bool CanSave() { return !string.IsNullOrEmpty(saveID); }

        // All child classes must implement this.
        // Remember to check CanSave() before you start saving.
        public abstract SaveData Save();
        public abstract bool Load(SaveData data);

        // Base class for all save data. All other save data must inherit from this.
        [Serializable]
        public class SaveData {
            public string saveID;

            // Printing out all fields of the SaveData object for debugging purposes.
            public override string ToString() {
                // Print out all the properties in the save data.
                Type type = GetType();
                StringBuilder output = new StringBuilder(GetClassName()).Append("\n");
                FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                foreach (FieldInfo field in fields) {
                    // Get the value of the property
                    object value = field.GetValue(this) ?? "null";
                    output.Append($"- {field.Name}: {value.ToString()}\n");
                }
                output.Remove(output.Length - 1, 1);

                return output.ToString();
            }
            
            public virtual string GetClassName() { return GetType().ToString(); }

            public virtual FieldInfo[] GetFields(BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy) {
                Type type = GetType();
                return type.GetFields(flags);
            }
            
        }
    }
}