using System;
using System.Collections.Concurrent;

namespace VoxelEngine.Threading {
    /// <summary>
    /// Handles processing of tasks by a thread pool.
    /// </summary>
    public class TaskManager : IDisposable {
        // Singleton Instance
        private static readonly object Padlock = new object();
        private static TaskManager _instance;
        public static TaskManager Instance {
            get {
                lock (Padlock) {
                    return _instance ?? (_instance = new TaskManager(Environment.ProcessorCount > 4 ? Environment.ProcessorCount - 2 : 2));
                }
            }
        }

        // Array of task workers
        private readonly TaskWorker[] _taskWorkers;
        
        // Task Blocking Collection
        private readonly BlockingCollection<Action> _tasks;
        
        // Public Data
        public int ThreadCount => _taskWorkers.Length;
        public int TaskCount => _tasks.Count;
        public bool GetWorkerState(int index) => _taskWorkers[index].IsBusy;
        
        /// <summary>
        /// Creates a new task manager with the specified thread count.
        /// </summary>
        private TaskManager(int threadCount = 4) {
            // Thread count must be greater than zero
            threadCount = threadCount > 0 ? threadCount : 1;
            
            // Initialize blocking collection
            _tasks = new BlockingCollection<Action>(new ConcurrentQueue<Action>());
            
            // Initialize worker array and workers
            _taskWorkers = new TaskWorker[threadCount];
            for (var i = 0; i < _taskWorkers.Length; i++) {
                _taskWorkers[i] = new TaskWorker(_tasks);
            }
        }
        
        /// <summary>
        /// Adds a task to be executed by the thread pool.
        /// </summary>
        /// <param name="task"></param>
        public void PushTask(Action task) {
            _tasks.Add(task);
        }

        /// <summary>
        /// Disposes of this task manager finalizing the collection.
        /// Worker threads will terminate automatically.
        /// </summary>
        public void Dispose() {
            _tasks?.CompleteAdding();
            _tasks?.Dispose();
        }
    }
}