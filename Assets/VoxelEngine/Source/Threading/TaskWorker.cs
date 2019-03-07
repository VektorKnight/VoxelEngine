using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace VoxelEngine.Threading {
    /// <summary>
    /// Represents a single worker which consumes tasks from a shared blocking collection.
    /// </summary>
    public class TaskWorker {
        // Task Collection Reference
        private readonly BlockingCollection<Action> _tasks;
        
        // Thread State
        public bool IsBusy { get; private set; }
        
        /// <summary>
        /// Creates a new task worker linked to the given task collection.
        /// </summary>
        /// <param name="tasks">The collection from which to consume tasks.</param>
        public TaskWorker(BlockingCollection<Action> tasks) {
            // Set task collection reference
            _tasks = tasks;
            
            // Start worker thread
            var workerStart = new ThreadStart(WorkLoop);
            var workerThread = new Thread(workerStart) { IsBackground = true };
            workerThread.Start();
        }

        /// <summary>
        /// Worker thread loop.
        /// </summary>
        private void WorkLoop() {
            foreach (var task in _tasks.GetConsumingEnumerable()) {
                IsBusy = true;
                try {
                    task.Invoke();
                }
                catch (Exception e) {
                    Debug.LogException(e);
                }
                IsBusy = false;
            }
        }
    }
}