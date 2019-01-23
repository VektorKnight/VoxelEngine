using UnityEngine;

namespace VektorLibrary.Utility {
    public class FpsCounter {
        
        // FPS Counter
        private const float FPS_PERIOD = 0.5f;
        private int _fpsAccum;
        private float _fpsNextPeriod;
        private int _currentFps;
        
        // Constructor
        public FpsCounter() {
            _fpsNextPeriod = Time.realtimeSinceStartup + FPS_PERIOD;
        }
        
        /// <summary>
        /// Update the FPS counter and get the latest FPS value.
        /// Should be called periodically by an Update() function.
        /// </summary>
        /// <returns>The current FPS value.</returns>
        public int UpdateValues() {
            // Calculate average FPS
            _fpsAccum++;
            if (!(Time.realtimeSinceStartup > _fpsNextPeriod)) return _currentFps;
            _currentFps = (int) (_fpsAccum / FPS_PERIOD);
            _fpsAccum = 0;
            _fpsNextPeriod += FPS_PERIOD;
            return _currentFps;
        } 
    }
}