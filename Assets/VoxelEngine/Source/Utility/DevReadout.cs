using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using VektorLibrary.Utility;

namespace VoxelEngine.Utility {
    /// <summary>
    /// Utility class for drawing various debug readouts to the game screen.
    /// </summary>
    public class DevReadout : MonoBehaviour {
        
        // Singleton Instance & Accessor
        public static DevReadout Instance { get; private set; }

        // Unity Inspector
        [Header("Debug Readout Config")] 
        [SerializeField] private KeyCode _toggleKey = KeyCode.F2;
        [SerializeField] private KeyCode _showTargetingKey = KeyCode.F3;
        [SerializeField] private Text _debugText;
        
        // Private: Debug Fields
        private readonly Dictionary<string, string> _debugFields = new Dictionary<string, string>();
        
        // Private: FPS Counter
        private FpsCounter _fpsCounter;
        
        // Private: State
        private bool _enabled;
        
        // Property: State
        public static bool Enabled => Instance._enabled;
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Preload() {
            // Load the prefab from the common objects folder
            var prefab = Resources.Load<Canvas>("Common/DebugReadout");
            
            // Destroy any existing instances
            if (Instance != null) Destroy(Instance.gameObject);
            
            // Instantiate and assign the instance
            var instance = Instantiate(prefab, Vector3.zero, Quaternion.identity);
            Instance = instance.GetComponentInChildren<DevReadout>();

            // Ensure this singleton does not get destroyed on scene load
            DontDestroyOnLoad(Instance.transform.root);
            
            // Initialize the instance
            Instance.Initialize();
        }

        // Initialization
        private void Initialize() {       
            // Set up some default readouts
            var version = Debug.isDebugBuild ? "DEVELOPMENT" : "DEPLOY";
            UpdateField("VektorKnight ", $"Voxel Engine [{version}]");
            
            UpdateField("CPU", $"{SystemInfo.processorCount} x {SystemInfo.processorType}");
            UpdateField("GPU", $"{SystemInfo.graphicsDeviceName} [{SystemInfo.graphicsDeviceType}]");
            
            AddField("FPS");
            
            _fpsCounter = new FpsCounter();
            _enabled = true;
        }
        
        // Toggle display the of readout
        public static void ToggleReadout() {
            if (Instance._enabled) {
                Instance._debugText.enabled = false;
                Instance._enabled = false;
            }
            else {
                Instance._debugText.enabled = true;
                Instance._enabled = true;
            }
        }
        
        // Toggle the display of the readout based on a bool
        public static void ToggleReadout(bool state) {
            if (state) {
                Instance._debugText.enabled = true;
                Instance._enabled = true;
            }
            else {
                Instance._debugText.enabled = false;
                Instance._enabled = false;
            }
        }
        
        // Add a debug field to the readout
        public static void AddField(string key) {
            // Exit if the specified key already exists
            if (Instance._debugFields.ContainsKey(key)) return;
            
            // Add the key to the dictionary with the given value
            Instance._debugFields.Add(key, "null");
        }
        
        // Remove a debug field from the readout
        public static void RemoveField(string key) {
            // Exit if the specified key does not exist
            if (!Instance._debugFields.ContainsKey(key)) return;
            
            // Remove the key from the dictionary
            Instance._debugFields.Remove(key);
        }
        
        // Update an existing debug field
        public static void UpdateField(string key, string value) {
            // Create a new field if the specified field doesn't exist
            if (!Instance._debugFields.ContainsKey(key))
                Instance._debugFields.Add(key, value);
            
            // Update the specified field with the new value
            Instance._debugFields[key] = value;
        }
        
        // Unity Update
        private void Update() {
            // Check for key presses
            if (Input.GetKeyDown(Instance._toggleKey)) ToggleReadout();
            
            // Exit if the readout is disabled
            if (!_enabled) return;
            
            // Update FPS Counter
            UpdateField("FPS", $"{Instance._fpsCounter.UpdateValues()} (Δ{Time.deltaTime * 1000f:n1}ms)");
            
            // Iterate through the debug fields and add them to the readout
            var displayText = new StringBuilder();
            foreach (var field in Instance._debugFields) {
                displayText.Append($"{field.Key}: {field.Value}\n");
            }
            
            // Set the readout text
            Instance._debugText.text = displayText.ToString();
        }
    }
}