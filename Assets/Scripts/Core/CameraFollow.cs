using UnityEngine;

namespace SilverFang.Core
{
    /// Dynamic combat camera: smooth follow with facing lookahead, duel zoom
    /// that tightens when the fight gets close, and punch-in zooms on kills,
    /// finishers, charged releases, and combo milestones. Punch timers run on
    /// unscaled time so HitStop slow-mo and the camera work together instead
    /// of stalling each other.
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float smoothTime = 0.15f;
        [SerializeField] private float minX = float.MinValue;
        [SerializeField] private float maxX = float.MaxValue;
        [SerializeField] private float fixedY = 0f;
        [SerializeField] private bool lockY = true;

        [Header("Dynamics")]
        [SerializeField] private float baseSize = 4f;
        [SerializeField] private float lookAhead = 1.5f;     // lead the player's movement
        [SerializeField] private float lookAheadSmooth = 0.4f;
        [SerializeField] private float duelZoom = 3.55f;     // tighter framing in close fights
        [SerializeField] private float duelRadius = 4.5f;
        [SerializeField] private float zoomSmooth = 0.35f;
        [SerializeField] private float maxPunch = 1.5f;      // hard cap on punch-in zoom

        public static CameraFollow Instance { get; private set; }

        private Camera cam;
        private float velocityX;
        private bool locked;
        private float lockedX;
        private float shakeTime;
        private float shakeAmplitude;

        private float lastTargetX;
        private float lookAheadCurrent;
        private float lookAheadVel;
        private float sizeVel;

        private float punchStrength;
        private float punchTimer;
        private float punchDuration = 0.5f;
        private Vector2 punchFocus;
        private bool hasPunchFocus;

        private Enemies.EnemyAI[] enemyCache = System.Array.Empty<Enemies.EnemyAI>();
        private float enemyCacheTimer;

        private void Awake()
        {
            Instance = this;
            cam = GetComponent<Camera>();
            if (cam != null) baseSize = cam.orthographicSize;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// Brief positional shake for heavy impacts.
        public void Shake(float amplitude = 0.12f, float duration = 0.16f)
        {
            shakeAmplitude = amplitude;
            shakeTime = duration;
        }

        public void LockAt(float x)
        {
            locked = true;
            lockedX = x;
        }

        public void Unlock() => locked = false;

        /// Retarget after the character-select screen picks a hero.
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            if (newTarget != null) lastTargetX = newTarget.position.x;
        }

        /// Temporary zoom-in toward the action. Strength 1 ~= -0.9 ortho size.
        /// Stronger requests override weaker ones; weaker ones never cut a
        /// big moment short.
        public void PunchIn(float strength, float duration = 0.5f, Vector2? focus = null)
        {
            strength = Mathf.Min(strength, maxPunch);
            if (strength < punchStrength * (punchTimer / Mathf.Max(punchDuration, 0.01f))) return;
            punchStrength = strength;
            punchDuration = Mathf.Max(duration, 0.1f);
            punchTimer = punchDuration;
            hasPunchFocus = focus.HasValue;
            if (focus.HasValue) punchFocus = focus.Value;
        }

        public void KillPunch(Vector2 pos) => PunchIn(1.1f, 0.55f, pos);
        public void FinisherPunch(Vector2 pos) => PunchIn(1.45f, 0.75f, pos);

        public void ComboPunch(int hits) =>
            PunchIn(Mathf.Min(0.45f + hits * 0.012f, 0.95f), 0.4f);

        private float NearestEnemyDistance(out Vector2 nearestPos)
        {
            nearestPos = default;
            if (target == null) return float.MaxValue;

            enemyCacheTimer -= Time.unscaledDeltaTime;
            if (enemyCacheTimer <= 0f)
            {
                enemyCacheTimer = 0.5f;
                enemyCache = Object.FindObjectsByType<Enemies.EnemyAI>();
            }

            float best = float.MaxValue;
            foreach (var enemy in enemyCache)
            {
                if (enemy == null || enemy.IsDead) continue;
                float d = Vector2.Distance(enemy.transform.position, target.position);
                if (d < best)
                {
                    best = d;
                    nearestPos = enemy.transform.position;
                }
            }
            return best;
        }

        private void LateUpdate()
        {
            if (target == null) return;

            float dt = Time.deltaTime;
            float udt = Time.unscaledDeltaTime;

            // --- lookahead: lead in the direction the hero is moving ---
            float velX = dt > 0.0001f ? (target.position.x - lastTargetX) / dt : 0f;
            lastTargetX = target.position.x;
            float desiredLead = Mathf.Clamp(velX / 4f, -1f, 1f) * lookAhead;
            lookAheadCurrent = Mathf.SmoothDamp(lookAheadCurrent, desiredLead, ref lookAheadVel,
                lookAheadSmooth, float.MaxValue, udt);

            // --- punch-in state (unscaled so slow-mo doesn't freeze it) ---
            float punch01 = 0f;
            if (punchTimer > 0f)
            {
                punchTimer -= udt;
                float t = Mathf.Clamp01(punchTimer / punchDuration);
                punch01 = t * t * (3f - 2f * t); // smooth ease-out
            }

            // --- framing: player + lookahead, pulled toward the action ---
            float targetX = locked ? lockedX : target.position.x + lookAheadCurrent;
            float nearest = NearestEnemyDistance(out Vector2 duelPos);
            if (!locked && nearest < duelRadius)
                targetX = Mathf.Lerp(targetX, (target.position.x + duelPos.x) * 0.5f,
                    1f - nearest / duelRadius);
            if (hasPunchFocus && punch01 > 0f)
                targetX = Mathf.Lerp(targetX, punchFocus.x, 0.4f * punch01);
            targetX = Mathf.Clamp(targetX, minX, maxX);

            float newX = Mathf.SmoothDamp(transform.position.x, targetX, ref velocityX, smoothTime);
            float newY = lockY ? fixedY : transform.position.y;
            if (hasPunchFocus && punch01 > 0f)
                newY += Mathf.Clamp((punchFocus.y - fixedY) * 0.25f, -0.6f, 0.6f) * punch01;

            // --- zoom: duel framing + punch-ins ---
            if (cam != null)
            {
                float desiredSize = baseSize;
                if (nearest < duelRadius)
                    desiredSize = Mathf.Lerp(duelZoom, baseSize, nearest / duelRadius);
                desiredSize -= 0.9f * punchStrength * punch01;
                desiredSize = Mathf.Max(2.6f, desiredSize);
                cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize, desiredSize,
                    ref sizeVel, zoomSmooth, float.MaxValue, udt);
            }

            if (shakeTime > 0f)
            {
                shakeTime -= udt;
                var jolt = Random.insideUnitCircle * shakeAmplitude * Mathf.Clamp01(shakeTime / 0.16f);
                newX += jolt.x;
                newY += jolt.y;
            }

            transform.position = new Vector3(newX, newY, transform.position.z);
        }

        public void SetBounds(float min, float max)
        {
            minX = min;
            maxX = max;
        }
    }
}
