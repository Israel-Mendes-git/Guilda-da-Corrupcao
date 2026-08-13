using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Terresquall {
    [RequireComponent(typeof(Image))][DisallowMultipleComponent]
    public class SaveIndicator : MonoBehaviour {

        public Image target;
        [Min(0f)] public float fadeTime = .25f, minActiveTime = .5f;
        public Sprite[] sprites;
        public uint frameRate = 12;

        int currentFrame = 0;
        float timeToNextFrame = 0f;
        bool isActive = false;

        int currentFadeDirection = 0;
        float currentFadeDuration = 0f, remainingFadeDuration = 0f, startAlpha = 1f, currentActiveDuration = 0f;
        Queue<bool> fadeQueue = new Queue<bool>();

        public bool CanAnimate() {
            if (!enabled) return false;
            if (!target || sprites == null || sprites.Length < 1) return false;
            if (frameRate < 1) return false;
            return true;
        }

        // Switches this on / off.
        public static void Toggle() { 
            if(instance) instance.SetActive(!instance.isActive);
        }

        public static void Toggle(bool active) {
            if(instance) instance.SetActive(active);
        }

        public void SetActive(bool active) {
            if (currentFadeDirection != 0) {
                fadeQueue.Enqueue(active);
                return;
            }

            if (active) {
                if (!target.isActiveAndEnabled) {
                    // Start fading in
                    target.gameObject.SetActive(true);
                    currentFadeDirection = 1; // 1 for fading in.
                    currentFadeDuration = remainingFadeDuration = fadeTime; // Example fade duration (in seconds)
                    currentFrame = 0;
                    currentActiveDuration = 0f;
                    target.enabled = true;
                }
            } else {
                if (currentActiveDuration > minActiveTime) {
                    // Start fading out if we are active for the min active duration.
                    if (target.isActiveAndEnabled) {
                        startAlpha = target.color.a;
                        currentFadeDirection = -1; // -1 for fading out.
                        currentFadeDuration = remainingFadeDuration = fadeTime; // Example fade duration (in seconds).
                    }
                } else currentFadeDirection = 0;
            }
            isActive = active;
        }

        public void HandleFade() {
            // If there is a queue, toggle the next action.
            if (fadeQueue.Count > 0)
                SetActive(fadeQueue.Dequeue());

            // Handle fading
            if (currentFadeDirection != 0) {
                remainingFadeDuration -= Time.deltaTime; // Decrement fade duration
                remainingFadeDuration = Mathf.Max(remainingFadeDuration, 0); // Clamp to avoid negative values

                float progress = remainingFadeDuration / currentFadeDuration;

                // Adjust alpha based on fade direction
                float alpha = (currentFadeDirection == 1) ? (startAlpha - progress) : progress * startAlpha;
                target.color = new Color(target.color.r, target.color.g, target.color.b, alpha);

                // Stop fading when completed
                if (remainingFadeDuration <= 0f) {
                    if (currentFadeDirection == -1) {
                        target.gameObject.SetActive(false); // Deactivate after fading out
                        isActive = false; // Mark as inactive
                    }
                    currentFadeDirection = 0; // Stop fading
                }
            }
        }

        public void HandleAnimation() {
            // If the image is inactive, skip animation
            if (!target || !target.isActiveAndEnabled) return;

            // Handle animation of the indicator.
            if (!CanAnimate()) return;

            timeToNextFrame -= Time.deltaTime;
            if (timeToNextFrame <= 0) {
                currentFrame = (currentFrame + 1) % sprites.Length; // Cycle through frames
                target.sprite = sprites[currentFrame]; // Update the displayed sprite
                timeToNextFrame += 1f / frameRate; // Reset frame timing
            }

            return;
        }

        public void Update() {
            // Handle the activation / deactivation of this object factoring in minActiveTime.
            // We try to continue counting the active duration and try to deactivate it again when time is reached.
            if (!isActive) {
                if (currentActiveDuration < minActiveTime)
                    currentActiveDuration += Time.deltaTime;
                else SetActive(false);
            }

            HandleFade();
            HandleAnimation();
        }

        public static SaveIndicator instance;

        void Reset() {
            target = GetComponent<Image>();
        }

        void Start() {
            target = GetComponent<Image>();
            // Delete any extra instances of this.
            if (instance && instance != this) {
                Debug.LogWarning("You have multiple instances of Save Indicator in your code. An extra instance has been removed.", this);
                Destroy(this);
                return;
            }

            instance = this;
            target.enabled = false; // Make sure the indicator is turned off.
        }

    }
}
