using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Terresquall {

    [CreateAssetMenu(fileName = "Bench Settings", menuName = "Bench Universal Save System/Settings")]
    public class Bench : ScriptableObject {

        public static bool debug = true; // Turn this on to show debug messages.
        public const string DEBUG_SEPARATOR = "-------------------\n";

        public static int currentSlot = 0; // Allows us to call SaveGame() with no arguments.
        public static double lastPlaytime = 0; // The playtime on the last save.
        public static double lastLoadTime = 0; // The Time.time when we last loaded the game.
        static string savePath => $"{Application.persistentDataPath}/saves/";

        public const string VERSION = "0.2.1";
        public const string LAST_UPDATED = "6 March 2026";

        [DataContract]
        public class SaveFile {
            [DataMember(Order = 0)]
            public Dictionary<string, string> metadata = new Dictionary<string, string>();
            [DataMember(Order = 1)]
            public List<PersistentObject.SaveData> data = new List<PersistentObject.SaveData>();
            [DataMember(Order = 2)]
            public int slot = -1;

            public System.Exception exception;

            public SaveFile(int slot, System.Exception exception = null) { 
                this.slot = slot;
                this.exception = exception;
            }

            public void PopulateMetadata() {
                if (metadata == null) metadata = new Dictionary<string, string>();
                metadata["time"] = System.DateTimeOffset.UtcNow.ToString();
                metadata["playtime"] = (lastPlaytime + Time.time - lastLoadTime).ToString();
                metadata["version"] = Application.version;
                metadata["unity_version"] = Application.unityVersion;
                metadata["current_scene_name"] = SceneManager.GetActiveScene().name;
                if (debug) Debug.Log(this);
            }

            public override string ToString() {
                // If successfully loaded, save the data in the list.
                StringBuilder sb = new StringBuilder($"Path: {GetSavePath(slot)}\n");
                sb.Append($"Slot: {slot}\n\n");

                // Display all recorded metadata.
                sb.Append($"Metadata\n");
                foreach (KeyValuePair<string, string> pair in metadata) {
                    sb.Append($"{pair.Key}: {pair.Value}\n");
                }
                sb.Append('\n');

                // Print out all saved PersistentObject data.
                foreach (PersistentObject.SaveData data in data) {
                    sb.Append(data.ToString()).Append('\n').Append('\n');
                }

                return sb.ToString();
            }
        }
        static SaveFile currentSaveFile;
        public static SaveFile GetCurrentSaveFile() { return currentSaveFile; }
        static List<System.Type> knownTypes;

        [Min(1)] public int maxSaveSlots = 3;
        public static string saveFormat = ".txt";

        [Header("Editor UI")]
        public bool showDebugLogs = false;

        // Events.
        public static event System.Action<SaveFile> OnFormatSave;

        // Transfer instance variables over to static variables.
        protected virtual void OnEnable() {
            debug = showDebugLogs;
        }

        // Returns a list of all the classes we will be saving.
        // Need for XML Serializer to work.
        static List<System.Type> GetKnownTypes() {
            if (knownTypes != null) return knownTypes;
            knownTypes = new List<System.Type>() {
                typeof(string), typeof(Dictionary<string, string>),
                typeof(PersistentObject.SaveData),
                typeof(List<PersistentObject.SaveData>)
            };
            knownTypes.AddRange(
                System.AppDomain.CurrentDomain.GetAssemblies().SelectMany(assembly => assembly.GetTypes())
                .Where(type => type.IsSubclassOf(typeof(PersistentObject.SaveData)) && !type.IsAbstract).ToList()
            );
            return knownTypes;
        }

        // Looks for the save data of a specific object.
        public static PersistentObject.SaveData Find(PersistentObject p) {
            if (currentSaveFile == null || currentSaveFile.data == null) return null;
            return currentSaveFile.data.Find(obj => obj.saveID == p.saveID);
        }
        
        public static PersistentObject.SaveData ReadAndFind(PersistentObject p, int slot = 0) {
            SaveFile save = ReadSaveFile(slot);
            if (save == null || save.data == null) return null;
            return save.data.Find(obj => obj.saveID == p.saveID);
        }

        // The asynchronous version of the QuickSave single target function.
        public static async Task<SaveFile> QuickSaveAsync(PersistentObject target) {
            await Task.Yield();
            return QuickSave(target);
        }

        // A simpler version that allows us to save the data only for 1 object.
        public static SaveFile QuickSave(PersistentObject target) {
            return QuickSave(new PersistentObject[] { target });
        }

        // An asynchronous version of the QuickSave function.
        public static async Task<SaveFile> QuickSaveAsync(PersistentObject[] targets = null) {
            SaveIndicator.Toggle(true);
            await Task.Yield();
            SaveFile result = QuickSave(targets);
            SaveIndicator.Toggle(false);
            return result;
        }

        public static MemoryStream FormatSave(SaveFile file = null) {
            // If we can't find a legitimate save file, then return null.
            if (file == null) {
                if (currentSaveFile == null) return null;
                file = currentSaveFile;
            }

            file.PopulateMetadata();
            OnFormatSave?.Invoke(file); // Filter for allowing other scripts to modify the save.

            // Serialize the data.
            DataContractSerializer data = new DataContractSerializer(typeof(SaveFile), GetKnownTypes());
            MemoryStream ms = new MemoryStream();
            data.WriteObject(ms, file);

            return ms; // Return the serialized data as a byte array
        }

        // Saves a copy of the file to the cache without saving onto the hard drive.
        // Returns the save data afterwards.
        public static SaveFile QuickSave(PersistentObject[] targets = null) {
            // If the cache is empty, create a new copy of it.
            if (currentSaveFile == null) currentSaveFile = new SaveFile(currentSlot);

            // If an array of objects to save is specified, we use that.
            // Otherwise we will save all of the objects in the Scene.
            PersistentObject[] saveables;
            if (targets != null) saveables = targets;
            else saveables = FindObjects<PersistentObject>(true);

            StringBuilder debugOutput = new StringBuilder();
            foreach (PersistentObject p in saveables) {
                // Ignore objects without valid save IDs.
                if (string.IsNullOrEmpty(p.saveID)) continue;

                PersistentObject.SaveData s = p.Save();
                if (s == null) continue;

                // Make sure that you add the object's save ID, in case the 
                // user forgets to define it in the Save() function of their object.
                if (string.IsNullOrEmpty(s.saveID)) s.saveID = p.saveID;

                // If there is no copy of the object in the cache, add it.
                // Otherwise, find a copy of the object in the cache and overwrite it.
                int index = currentSaveFile.data.FindIndex(item => item.saveID == s.saveID);
                if (index < 0) currentSaveFile.data.Add(s);
                else currentSaveFile.data[index] = s;

                // Print the saved contents if debug is turned on.
                if (debug)
                    debugOutput.Append(
                        $"Saving data for GameObject \"{p.name}\" with save ID {p.saveID}.\n"
                    ).Append(s).Append('\n').Append(DEBUG_SEPARATOR);
            }

            // Outputs the debug data.
            if (debugOutput.Length > 0)
                Debug.Log($"Quick save on slot {currentSlot}. Added data for the following:\n{DEBUG_SEPARATOR}{debugOutput}");

            return currentSaveFile;
        }

        // Loads the game from the cache.
        public static bool QuickLoad(PersistentObject[] targets = null) {
            if (currentSaveFile == null) return false;

            StringBuilder debugOutput = new StringBuilder(); // Debug string if we are debugging.

            // Determines the objects that we want to load data into.
            // Loads everything by default.
            PersistentObject[] saveables;
            if (targets != null) saveables = targets;
            else saveables = FindObjects<PersistentObject>();

            // The actual loading work.
            foreach (PersistentObject p in saveables) {
                QuickLoad(p);
            }

            if (debug) {
                if (debugOutput.Length > 0)
                    Debug.Log($"Quick load on slot {currentSlot}. The following data was loaded:\n{DEBUG_SEPARATOR}{debugOutput}"); // Marks the end of the load function.
                else Debug.Log($"Quick load on slot {currentSlot}, but no loadable data was found.");
            }
            return true;
        }
        
        public static bool QuickLoad(PersistentObject target, StringBuilder debugOutput = null) {

            if (string.IsNullOrEmpty(target.saveID)) return false;

            PersistentObject.SaveData data = Find(target);

            // Put the values into the object.
            if (data != null) {
                if (debug && debugOutput != null)
                    debugOutput.Append(
                        $"Loaded save data onto GameObject \"{target.name}\" with save ID {target.saveID}.\n"
                    ).Append(target).Append('\n').Append(DEBUG_SEPARATOR); // Print debug output.
                return target.Load(data);
            }

            // We couldn't find a valid data to load for the given object.
            if (debug && debugOutput != null)
                debugOutput.Append(
                    string.Format(
                        "No save data found for {0} component on GameObject \"{1}\" with save ID {2}.\n",
                        target.GetType().ToString(), target.name,
                        (string.IsNullOrEmpty(target.saveID) ? "that is missing" : target.saveID)
                    )
                ).Append(DEBUG_SEPARATOR);
            return false;

        }

        // Shorthand for saving onto the current slot.
        public static void SaveGame(bool reprocessSave) { SaveGame(-1, reprocessSave); }

        // Saves the game into persistent memory.
        public static void SaveGame(int id = -1, bool reprocessSave = true) {
            if (id < 0) id = currentSlot;

            // Ensure the currents ave file has the latest data
            if (reprocessSave || currentSaveFile == null) currentSaveFile = QuickSave();

            // Format the save.
            MemoryStream data = FormatSave();

            // Ensures our save directory exists.
            Directory.CreateDirectory(savePath);

            // Save the file to a stream.
            string path = GetSavePath(id);
            using (FileStream stream = File.Open(path, FileMode.Create)) {
                data.Position = 0;
                data.CopyTo(stream);
            }

            if (debug) Debug.Log($"Saved data to {path}.");

            data.Dispose();
        }

        public static async void SaveGameAsync(bool reprocessSave) {
            await Task.Run(() => SaveGameAsync(currentSlot, reprocessSave));
        }

        public static async void SaveGameAsync(int id = -1, bool reprocessSave = true) {
            SaveIndicator.Toggle(true);

            if (id < 0) id = currentSlot;

            // Ensure the cache is filled with the latest data
            if (reprocessSave || currentSaveFile == null) currentSaveFile = QuickSave();

            MemoryStream data = null;
            await Task.Yield();

            data = FormatSave();
            if (data == null) return;

            // Ensures our save directory exists.
            Directory.CreateDirectory(savePath);

            // Create the binary formatter to save the file.
            string path = GetSavePath(id);
            using (FileStream stream = File.Open(path, FileMode.Create)) {
                data.Position = 0;
                await data.CopyToAsync(stream);
            }

            if (debug) Debug.Log($"Saved (async) data to {path}.");

            await Task.Run(() => data.Dispose());
            SaveIndicator.Toggle(false);
        }

        // Extracts only the metadata from the save file.
        public static Dictionary<string, string> PeekGame(int id) {
            // Don't return anything if there is no save.
            if (!SlotHasSave(id)) return null;

            Dictionary<string, string> metadata = new Dictionary<string, string>();
            using (XmlReader reader = XmlReader.Create(GetSavePath(id))) {
                bool inMetadata = false;
                string currentKey = null;

                // Begin reading through the document.
                while (reader.Read()) {
                    if (reader.NodeType == XmlNodeType.Element) {
                        // We only want to read the metadata tag.
                        // Once it is read and we've hit data
                        if (reader.LocalName == "metadata") {
                            inMetadata = true;
                        } else {
                            // If we are inside the metadata tag, extract the keys and values.
                            if (inMetadata) {
                                if (reader.LocalName == "Key") {
                                    currentKey = reader.ReadElementContentAsString();
                                    if (!string.IsNullOrWhiteSpace(currentKey)) {
                                        metadata[currentKey] = reader.ReadElementContentAsString();
                                        currentKey = null;
                                    }
                                } else if (reader.LocalName == "data")
                                    break; // If we've read already read metadata and are reading data now, exit.
                            } else if (reader.LocalName == "data") continue; // If not, continue reading.

                        }
                    }
                }
            }

            return metadata;
        }

        // Loads the game on a specific slot into the cache.
        public static SaveFile LoadGame(int id = 0) {
            currentSlot = Mathf.Max(0, id); // Saves the current slot, so we save into the same slot later.

            currentSaveFile = ReadSaveFile(id);
            if (currentSaveFile != null) QuickLoad();
            return currentSaveFile;
        }

        public static SaveFile ReadSaveFile(int slot) {
            slot = Mathf.Max(0, slot);
            if (!SlotHasSave(currentSlot))
                return null;

            // Load and read the file.
            DataContractSerializer data = new DataContractSerializer(typeof(SaveFile), GetKnownTypes());
            try {
                using (FileStream stream = File.Open(GetSavePath(slot), FileMode.Open)) {
                    // Assign the file into the cache, and use QuickLoad() to load it.
                    try {
                        currentSaveFile = (SaveFile)data.ReadObject(stream);
                    } catch (SerializationException e) {
                        Debug.LogWarning($"Failed to read save file from slot {slot}: {e.Message}");
                        return new SaveFile(slot, e);
                    }

                    return currentSaveFile;
                }
            } catch(FileNotFoundException e) {
                Debug.LogWarning($"Failed to read save file from slot {slot}: {e.Message}");
                return new SaveFile(slot, e);
            }
        }

        // Deletes a specific save slot.
        public static bool Delete(int id) {
            string path = GetSavePath(id);
            if (!File.Exists(path)) {
                Debug.Log($"No save file in {path}.");
                return false;
            }

            File.Delete(path);
            if (File.Exists(path)) {
                Debug.LogWarning($"{path} has not been successfully deleted.");
                return false;
            }

            Debug.Log($"{path} successfully cleared.");
            currentSaveFile = null;
            return true;
        }

        public static int CountSaves() {
            return Directory.GetFiles(savePath).Length;
        }

        public static string GetSavePath() { return savePath; }

        // Gets the path of a save file.
        public static string GetSavePath(int slot) { return $"{savePath}{slot}{saveFormat}"; }

        // Checks if a given slot has a save file.
        public static bool SlotHasSave(int slot) {
            if (slot < 0) slot = Mathf.Max(0, currentSlot);
            return File.Exists(GetSavePath(slot));
        }

        public static T FindObject<T>(bool includeInactive = false) where T : Object {
#if UNITY_2023_1_OR_NEWER
            return FindAnyObjectByType<T>(includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude);
#else
            return FindObjectOfType<T>(includeInactive);
#endif
        }

        public static T[] FindObjects<T>(bool includeInactive = false) where T : Object {
#if UNITY_2023_1_OR_NEWER
            return FindObjectsByType<T>(includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
            return FindObjectsOfType<T>(includeInactive);
#endif
        }

        // Find an object in the Scene by save ID.
        public static PersistentObject FindObjectBySaveID(string saveID) {
            PersistentObject[] objects = FindObjects<PersistentObject>(true);
            foreach(PersistentObject p in objects) {
                if (p.saveID == saveID) return p;
            }
            return null;
        }

        public static T FindObjectBySaveID<T>(string saveID) where T : PersistentObject {
            return FindObjectBySaveID(saveID) as T;
        }
    }
}
