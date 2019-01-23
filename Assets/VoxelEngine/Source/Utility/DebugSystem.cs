using System;
using Windows;
using UnityEngine;

namespace RTSEngine {
    /// <summary>
    /// Creates and manages the console window and in-game overlay for debugging info.
    /// </summary>
    internal class DebugSystem : MonoBehaviour {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN

        // Console Fields
        private readonly ConsoleWindow _consoleWindow = new ConsoleWindow();
        private readonly ConsoleInput _consoleInput = new ConsoleInput();

        // Singleton pattern
        public static DebugSystem Instance;

        /// <summary>
        /// Preload and ensure singleton
        /// </summary>
        [RuntimeInitializeOnLoadMethod]
        public static void Initialize() {
            //Make sure the Managers object exists
            var systems = GameObject.Find("Systems") ?? new GameObject("Systems");

            // Ensure this singleton initializes at startup
            if (Instance == null) Instance = systems.GetComponent<DebugSystem>() ?? systems.AddComponent<DebugSystem>();

            // Ensure this singleton does not get destroyed on scene load
            DontDestroyOnLoad(Instance.gameObject);

            Instance.Setup();
        }

        // Unity Monobehavior Callbacks

        #region Unity Monobehavior Callbacks

        // Unity OnEnable Callback
        private void Setup() {
            DontDestroyOnLoad(gameObject);

            _consoleWindow.Initialize();
            _consoleWindow.SetTitle("Voxel Engine");

            Application.logMessageReceived += HandleLog;

            Debug.Log("Debug Console Enabled!");
        }

        // Unity Update Callback
        private void Update() {
            _consoleInput.Update();
        }

        // Unity OnDisable Callback
        private void OnDisable() {
            Application.logMessageReceived -= HandleLog;
        }

        // Unity OnDestroy Callback
        private void OnDestroy() {
            _consoleWindow.Shutdown();
        }

        #endregion

        // Message Handling Functions

        #region Message Handlers

        // MessageReceived Event Handler (Unity Redirect)
        private void HandleLog(string message, string stackTrace, LogType type) {
            // Fetch the current time
            var time = DateTime.Now.ToString("hh:mm:ss");

            // Set based on the log type
            var logTrace = false;

            // Handle primary message types
            switch (type) {
                case LogType.Warning:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;
                case LogType.Error:
                    Console.ForegroundColor = ConsoleColor.Red;
                    logTrace = true;
                    break;
                case LogType.Exception:
                    Console.ForegroundColor = ConsoleColor.Red;
                    logTrace = true;
                    break;
                default:
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
            }

            // Log messages to the console
            if (logTrace) {
                Console.WriteLine($"[{time}] {message}\n");
                Console.WriteLine("-BEGIN STACK TRACE-\n");
                Console.WriteLine(stackTrace);
                Console.WriteLine("-END STACK TRACE-\n");
            }
            else {
                Console.WriteLine($"[{time}] {message}");
            }

            // Make sure we don't lose user input
            _consoleInput.RedrawInputLine();
        }

        /// <summary>
        /// Logs a message directly to the console window without using the Unity Debug interface.
        /// Much faster as a stacktrace is not included.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="type">The type of message (determines color).</param>
        public void LogDirect(string message, LogType type = LogType.Log) {
            // Handle primary message types
            switch (type) {
                case LogType.Warning:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;
                case LogType.Error:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
                case LogType.Exception:
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    break;
                default:
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
            }

            // Log messages to the console
            Console.WriteLine(message);

            // Make sure we don't lose user input
            _consoleInput.RedrawInputLine();
        }

        #endregion

#endif
    }
}