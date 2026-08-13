using UnityEngine;
using TMPro;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor.Events;
#endif

namespace Terresquall {
    [RequireComponent(typeof(Button))]
    public class UISaveSlot : MonoBehaviour {

        public TextMeshProUGUI slotNumberUI;
        public TextMeshProUGUI metadataUI;
        public Button deleteButtonUI;
        public string defaultScene = "SampleScene";
        public bool disableIfEmpty = true;

        [Header("Metadata")]
        public string displayFormat = "{0}: {1}";
        public string dateFormat = "d MMM yyyy";
        public string playtimeFormat = @"h'h 'mm'm 'ss's'";

        [System.Serializable]
        public struct MetadataMapping { public string key, display; };
        public MetadataMapping[] mappings = new MetadataMapping[] {
            new MetadataMapping{ key = "current_scene_name", display = "Scene" },
            new MetadataMapping{ key = "time", display = "Time" },
            new MetadataMapping{ key = "playtime", display = "Playtime" },
            new MetadataMapping{ key = "version", display = "Version" },
            new MetadataMapping{ key = "unity_version", display = "Unity Version" }
        };

        public enum MetadataDelimiter { comma, newline }
        public MetadataDelimiter delimiter = MetadataDelimiter.newline;

        public delegate void ProcessUpdate(int slot, Dictionary<string, string> metadata, string slotDisplay);
        public ProcessUpdate processUpdate;

        public delegate string ProcessMetadataKey(string key, string value);
        public ProcessMetadataKey processMetadataKey;
        public delegate string ProcessMetadataValue(string key, string value);
        public ProcessMetadataValue processMetadataValue;

        [Header("Messages")]
        [TextArea] public string noSaveDataFound = "No save data.";

        protected Dictionary<string, string> saveMetadata;
        protected Button button;
        protected bool isLoading = false;
        protected int targetSlot = 0;

        void Awake() {
            processUpdate += HandleProcessUpdate;
            processMetadataKey += HandleMetadataKey;
            processMetadataValue += HandleMetadataValue;
            button = GetComponent<Button>();
        }

        void Reset() {
            // Attempts to find relevant components.
            TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>();
            foreach (TextMeshProUGUI t in texts) {
                switch (t.name) {
                    case "Slot Number":
                        slotNumberUI = t;
                        break;
                    case "Metadata":
                        metadataUI = t;
                        break;
                }
            }

            // Find all the buttons.
            Button[] buttons = GetComponentsInChildren<Button>();
            foreach(Button b in buttons) {

                // If we find ourself, assign the button variable.
                if (b.transform == transform) {
                    button = b;
                }

                // Otherwise find and assign other relevant components.
                switch(b.name) {
                    case "Delete":
                        deleteButtonUI = b;
                        break;
                }
            }

#if UNITY_EDITOR
            if (!button) button = GetComponent<Button>();

            // Don't add the event if it has already been added.
            bool addLoad = true, addDelete = true;
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++) {
                if (button.onClick.GetPersistentMethodName(i) == "Load") addLoad = false;
                if (button.onClick.GetPersistentMethodName(i) == "Delete") addDelete = false;
            }
            if(addLoad) UnityEventTools.AddFloatPersistentListener(button.onClick, Load, 0f);
            if(addDelete && deleteButtonUI) UnityEventTools.AddPersistentListener(deleteButtonUI.onClick, Delete);
#endif
        }

        public void HandleProcessUpdate(int slot, Dictionary<string, string> saveMeta, string slotDisplay = null) {

            if (string.IsNullOrEmpty(slotDisplay)) slotNumberUI.text = $"{slot}.";
            else slotNumberUI.text = slotDisplay;
            targetSlot = slot;
            
            bool saveExists = saveMeta != null;
            if (saveExists) {
                string delimiter = ", ";
                switch (this.delimiter) {
                    case MetadataDelimiter.newline:
                        delimiter = "\n";
                        break;
                }

                // Grab all metadata to be shown.
                StringBuilder sb = new StringBuilder();
                foreach(MetadataMapping m in mappings) {
                    if(saveMeta.ContainsKey(m.key)) {
                        string key = processMetadataKey == null ? HandleMetadataKey(m.key, saveMeta[m.key]) : processMetadataKey(m.key, saveMeta[m.key]),
                               val = processMetadataValue == null ? HandleMetadataValue(m.key, saveMeta[m.key]) : processMetadataValue(m.key, saveMeta[m.key]);
                        sb.Append(string.Format(displayFormat, key, val)).Append(delimiter);
                    } else {
                        if (string.IsNullOrWhiteSpace(m.key)) sb.Append(m.display.Replace(@"\n", "\n"));
                    }
                }
                metadataUI.text = sb.ToString();
                saveMetadata = saveMeta;

            } else {
                metadataUI.text = noSaveDataFound;
                if(button && disableIfEmpty) button.interactable = false;
            }

            // Hide the delete button if the save does not exist.
            if (deleteButtonUI) deleteButtonUI.gameObject.SetActive(saveExists);
        }

        public string HandleMetadataKey(string key, string value) {
            return mappings.FirstOrDefault(m => m.key == key).display;
        }

        public string HandleMetadataValue(string key, string value) {
            switch(key) {
                case "time":
                    System.DateTime dt = System.DateTime.Parse(value);
                    return dt.ToString(dateFormat);
                case "playtime":
                    System.TimeSpan time = System.TimeSpan.FromSeconds((int)float.Parse(value));
                    return time.ToString(playtimeFormat);
            }
            return value;
        }

        public void Load(float delay = 0) {
            if (isLoading) return;

            // Mark isLoading so we cannot load multiple times.
            isLoading = true;
            StartCoroutine( HandleLoadCoroutine(delay) );
        }

        public void Delete() {
            Bench.Delete(targetSlot);
            saveMetadata = null;

            // Update the UI after deleting the file.
            UISaveSlotManager manager = GetComponentInParent<UISaveSlotManager>();
            if(manager) manager.UpdateSaveSlots();
        }

        protected IEnumerator HandleLoadCoroutine(float delay) {
            if (delay > 0) yield return new WaitForSeconds(delay);

            Bench.currentSlot = targetSlot; // Record our current slot.

            // If there is a save file.
            if (saveMetadata != null && saveMetadata.ContainsKey("current_scene_name")) {
                Bench.LoadGame(Bench.currentSlot);
                if(saveMetadata.ContainsKey("playtime"))
                    Bench.lastPlaytime = double.Parse(saveMetadata["playtime"]);
                SceneManager.LoadScene(saveMetadata["current_scene_name"]);
            } else {
                SceneManager.LoadScene(defaultScene);
            }

            // Update the last load time.
            Bench.lastLoadTime = Time.time;
        }

    }
}
