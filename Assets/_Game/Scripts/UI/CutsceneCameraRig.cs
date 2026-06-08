using UnityEngine;

namespace Mixtape.UI
{
    /// <summary>
    /// Drives a cutscene Cinemachine camera's position along a slow, eased dolly arc
    /// between two marker Transforms while Cinemachine keeps it framed on the subject
    /// (RotationComposer) and adds hand-held noise. Put this on the CinemachineCamera
    /// GameObject; the camera's Aim handles rotation, this only moves position so the
    /// two responsibilities never fight. Drag the start/end markers in the Scene view
    /// to re-shape the move.
    /// </summary>
    [DisallowMultipleComponent]
    public class CutsceneCameraRig : MonoBehaviour
    {
        [Header("Dolly arc (drag these markers in the Scene)")]
        public Transform startPose;
        public Transform endPose;

        [Tooltip("Seconds for the dolly to ease from the start marker to the end marker (one leg).")]
        public float duration = 8f;
        [Tooltip("When true the dolly eases start->end->start->... forever; when false it eases once and holds at the end.")]
        public bool pingPong = true;
        [Tooltip("Tiny breathing drift so it never freezes after settling (metres).")]
        public float settleDrift = 0.12f;
        [Tooltip("How slowly the breathing drift oscillates.")]
        public float driftSpeed = 0.18f;
        [Tooltip("Unscaled time so the move ignores any time-scale changes.")]
        public bool useUnscaledTime = true;

        private float _t;

        private void OnEnable()
        {
            _t = 0f;
            if (startPose != null) transform.position = startPose.position;
        }

        private void Update()
        {
            if (startPose == null || endPose == null) return;

            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            _t += dt;

            float cycle = duration > 0.0001f ? _t / duration : 1f;
            // pingPong: oscillate 0->1->0... (a full round trip is 2*duration); else 0->1 then hold.
            float p = pingPong ? Mathf.PingPong(cycle, 1f) : Mathf.Clamp01(cycle);
            float eased = p * p * (3f - 2f * p); // smoothstep => eases in AND out at each end
            Vector3 pos = Vector3.LerpUnclamped(startPose.position, endPose.position, eased);

            // Gentle vertical breathing on top of Cinemachine's hand-held noise.
            float drift = (Mathf.PerlinNoise(7.3f, _t * driftSpeed) - 0.5f) * 2f * settleDrift;
            pos.y += drift;

            transform.position = pos;
        }
    }
}
