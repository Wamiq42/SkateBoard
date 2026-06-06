using System.Collections.Generic;
using UnityEngine;

namespace Mixtape.Gameplay
{
    /// <summary>
    /// Spins the skateboard's wheels. Auto-finds wheel child meshes (the material-split
    /// corner pieces) whose pivots are already at each wheel center, and rolls them about
    /// their local X (width) axis. Speed can be set directly or driven externally (e.g. by
    /// the rider's current speed) via <see cref="SetSpeed"/>.
    /// </summary>
    public class WheelSpinner : MonoBehaviour
    {
        [Tooltip("Spin rate in degrees/second when running at the reference speed.")]
        public float degreesPerSecond = 720f;

        [Tooltip("Wheel transforms. Auto-populated on Awake if left empty.")]
        public Transform[] wheels;

        private float _rate;

        private void Reset() { AutoFind(); }

        private void Awake()
        {
            if (wheels == null || wheels.Length == 0) AutoFind();
            _rate = degreesPerSecond;
        }

        private void AutoFind()
        {
            var list = new List<Transform>();
            foreach (var t in GetComponentsInChildren<Transform>())
            {
                // Material-split wheels are named like "Board_Board_Mat_0.001" .. ".004".
                if (t.name.Contains("_0.0")) list.Add(t);
            }
            wheels = list.ToArray();
        }

        /// <summary>Drive the spin rate from gameplay (e.g. rider speed). </summary>
        public void SetSpeed(float degreesPerSecond) { _rate = degreesPerSecond; }

        private void Update()
        {
            float d = _rate * Time.deltaTime;
            if (wheels == null) return;
            for (int i = 0; i < wheels.Length; i++)
                if (wheels[i] != null) wheels[i].Rotate(Vector3.right, d, Space.Self);
        }
    }
}
