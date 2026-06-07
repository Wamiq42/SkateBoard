using UnityEngine;
using Mixtape.InputCtrl;

namespace Mixtape.Gameplay
{
    /// <summary>Feeds <see cref="InputService"/> (touch + keyboard) into the player's
    /// <see cref="PhysicsSkater"/>. AI uses <see cref="AISkater"/> instead.</summary>
    [RequireComponent(typeof(PhysicsSkater))]
    public class PlayerSkaterInput : MonoBehaviour
    {
        public float boostMultiplier = 1.5f;
        private PhysicsSkater _ps;
        private RiderAnimDriver _anim;

        private void Awake() => _ps = GetComponent<PhysicsSkater>();

        private void Update()
        {
            var input = InputService.Instance;
            if (input == null) return;

            _ps.SteerInput = input.Steer;
            if (input.JumpPressedThisFrame) _ps.JumpRequested = true;
            if (input.BoostHeld) _ps.ApplyBoost(boostMultiplier, 0.2f);
            if (input.TrickPressedThisFrame)
            {
                // RiderAnimDriver is added at runtime by RiderVisual, so fetch it lazily.
                if (_anim == null) _anim = GetComponent<RiderAnimDriver>();
                _anim?.Trick();   // trick jump: jump + board kickflip (F / down arrow)
            }
        }
    }
}
