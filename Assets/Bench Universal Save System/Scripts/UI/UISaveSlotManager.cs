using UnityEngine;
using UnityEngine.UI;

namespace Terresquall {
    public class UISaveSlotManager : MonoBehaviour {

        public Bench settings;
        public UISaveSlot template;
        public UISaveSlot[] slots;

        [Header("Numbering")]
        [Min(0)] public int startNumber = 0;

        void Reset() {
            template = GetComponentInChildren<UISaveSlot>();
        }

        public void UpdateSaveSlots() {
            for(int i = 0; i < slots.Length; i++) {
                UISaveSlot s = slots[i];
                s.HandleProcessUpdate(i, Bench.PeekGame(i), (i + startNumber).ToString());
            }
        }

        void Start() {
            UpdateSaveSlots();
        }

    }
}
