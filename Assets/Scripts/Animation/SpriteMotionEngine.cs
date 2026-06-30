using UnityEngine;

namespace SilverFang.Animation
{
    /// Silver Fang's Sprite Animation Engine (motion layer).
    ///
    /// Procedural secondary motion layered ON TOP of the baked sprite clips and
    /// COUPLED to the Combat Physics Engine: it reads the body's velocity and
    /// adds lean-into-motion, settle sway, and hit/land recoil so the large hand-
    /// drawn sprite library reads with weight and life without needing extra
    /// drawn frames. Lives on the Visual child; it only drives local rotation and
    /// a transient scale punch, so it never fights the clip's sprite swaps or the
    /// charge/attack squash the controller applies.
    [DefaultExecutionOrder(120)]
    public class SpriteMotionEngine : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D body;       // the character's physics body
        [SerializeField] private float leanPerSpeed = 0.9f; // degrees of tilt per unit of vx
        [SerializeField] private float maxLean = 9f;        // tilt clamp
        [SerializeField] private float leanSmoothing = 9f;  // how fast the lean eases
        [SerializeField] private float recoilDamp = 12f;    // how fast a recoil punch settles

        private float lean;
        private float recoil;   // transient rotational kick (hit / hard landing)
        private float recoilDir = 1f;

        private void Awake()
        {
            if (body == null) body = GetComponentInParent<Rigidbody2D>();
        }

        // Rotation-only so it never fights the controller's scale (facing flip,
        // charge squash, attack stretch).
        private void LateUpdate()
        {
            float vx = body != null ? body.linearVelocity.x : 0f;
            float facing = Mathf.Sign(transform.localScale.x == 0f ? 1f : transform.localScale.x);
            float targetLean = Mathf.Clamp(-vx * leanPerSpeed, -maxLean, maxLean) * facing;
            lean = Mathf.Lerp(lean, targetLean, Mathf.Clamp01(leanSmoothing * Time.deltaTime));

            recoil = Mathf.MoveTowards(recoil, 0f, recoilDamp * Time.deltaTime);

            var e = transform.localEulerAngles;
            transform.localEulerAngles = new Vector3(e.x, e.y, lean + recoil * 14f * recoilDir);
        }

        /// Rotational kick on a landed hit or hard landing (0..1 strength).
        public void Recoil(float strength, float dir = -1f)
        {
            recoil = Mathf.Max(recoil, Mathf.Clamp01(strength));
            recoilDir = Mathf.Sign(dir == 0f ? -1f : dir);
        }
    }
}
