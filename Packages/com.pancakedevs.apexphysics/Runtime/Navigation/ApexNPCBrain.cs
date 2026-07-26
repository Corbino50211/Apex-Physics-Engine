using System;
using UnityEngine;
using UnityEngine.AI;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// High-level behavior controller for a basic Apex NPC. It can idle, wander
    /// around a remembered home point, chase an assigned target, and retaliate
    /// against rigidbodies that hit the NPC hard enough.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ApexBody), typeof(ApexNPCNavigator), typeof(ApexNPCMotor))]
    public sealed class ApexNPCBrain : MonoBehaviour
    {
        [Header("Mode")]
        [SerializeField] private ApexNPCBehaviorMode startingMode = ApexNPCBehaviorMode.Wander;
        [SerializeField] private Transform chaseTarget;

        [Header("Wander")]
        [SerializeField, Min(0.1f)] private float wanderRadius = 8f;
        [SerializeField, Min(0.1f)] private float wanderSampleDistance = 3f;
        [SerializeField, Min(1)] private int wanderSampleAttempts = 12;
        [SerializeField, Min(0f)] private float minimumWanderWait = 0.5f;
        [SerializeField, Min(0f)] private float maximumWanderWait = 2f;

        [Header("Hard-Impact Retaliation")]
        [SerializeField] private bool chaseWhenHit = true;
        [SerializeField, Min(0f)] private float minimumImpactSpeed = 4f;
        [SerializeField, Min(0f)] private float minimumImpactImpulse = 2f;
        [SerializeField, Min(0f)] private float impactChaseDuration = 10f;
        [SerializeField] private ApexNPCBehaviorMode modeAfterImpactChase = ApexNPCBehaviorMode.Wander;

        private ApexBody cachedBody;
        private ApexNPCNavigator cachedNavigator;
        private ApexNPCBehaviorMode currentMode;
        private Vector3 homePosition;
        private float chaseEndTime = float.PositiveInfinity;
        private float nextWanderTime;
        private bool waitingForWanderDestination;

        public event Action<ApexNPCBehaviorMode> ModeChanged;
        public event Action<Transform> ChaseStarted;
        public event Action<Transform> ImpactTargetDetected;

        public ApexNPCBehaviorMode CurrentMode => currentMode;
        public Transform ChaseTarget => chaseTarget;
        public Vector3 HomePosition => homePosition;
        public float RemainingImpactChaseTime =>
            currentMode == ApexNPCBehaviorMode.Chase && !float.IsPositiveInfinity(chaseEndTime)
                ? Mathf.Max(0f, chaseEndTime - Time.time)
                : 0f;

        private void Reset()
        {
            CacheReferences();
            homePosition = transform.position;
        }

        private void Awake()
        {
            CacheReferences();
            homePosition = transform.position;
        }

        private void OnEnable()
        {
            CacheReferences();
            if (cachedBody != null)
            {
                cachedBody.Impacted += HandleImpact;
            }
        }

        private void Start()
        {
            SetMode(startingMode);
        }

        private void OnDisable()
        {
            if (cachedBody != null)
            {
                cachedBody.Impacted -= HandleImpact;
            }
        }

        private void Update()
        {
            switch (currentMode)
            {
                case ApexNPCBehaviorMode.Wander:
                    UpdateWander();
                    break;

                case ApexNPCBehaviorMode.Chase:
                    UpdateChase();
                    break;
            }
        }

        /// <summary>Changes the active behavior mode.</summary>
        public void SetMode(ApexNPCBehaviorMode newMode)
        {
            CacheReferences();

            if (newMode == ApexNPCBehaviorMode.Chase && chaseTarget == null)
            {
                newMode = ApexNPCBehaviorMode.Idle;
            }

            bool changed = currentMode != newMode;
            currentMode = newMode;

            switch (currentMode)
            {
                case ApexNPCBehaviorMode.Idle:
                    chaseEndTime = float.PositiveInfinity;
                    waitingForWanderDestination = false;
                    cachedNavigator?.SetTarget(null);
                    break;

                case ApexNPCBehaviorMode.Wander:
                    chaseTarget = null;
                    chaseEndTime = float.PositiveInfinity;
                    cachedNavigator?.SetTarget(null);
                    waitingForWanderDestination = true;
                    nextWanderTime = Time.time;
                    break;

                case ApexNPCBehaviorMode.Chase:
                    chaseEndTime = float.PositiveInfinity;
                    cachedNavigator?.SetTarget(chaseTarget);
                    ChaseStarted?.Invoke(chaseTarget);
                    break;
            }

            if (changed)
            {
                ModeChanged?.Invoke(currentMode);
            }
        }

        /// <summary>Starts chasing a target indefinitely until another mode is selected.</summary>
        public void SetChaseTarget(Transform target)
        {
            chaseTarget = target;
            chaseEndTime = float.PositiveInfinity;
            SetMode(target != null ? ApexNPCBehaviorMode.Chase : ApexNPCBehaviorMode.Idle);
        }

        /// <summary>Starts a timed chase and then returns to the configured post-impact mode.</summary>
        public void BeginTimedChase(Transform target, float duration)
        {
            if (target == null)
            {
                return;
            }

            chaseTarget = target;
            bool changed = currentMode != ApexNPCBehaviorMode.Chase;
            currentMode = ApexNPCBehaviorMode.Chase;
            chaseEndTime = Time.time + Mathf.Max(0f, duration);
            waitingForWanderDestination = false;
            cachedNavigator?.SetTarget(chaseTarget);

            ChaseStarted?.Invoke(chaseTarget);
            if (changed)
            {
                ModeChanged?.Invoke(currentMode);
            }
        }

        /// <summary>Uses the NPC's current position as the center of future wandering.</summary>
        public void SetHomeHere()
        {
            homePosition = transform.position;
        }

        public void SetHomePosition(Vector3 position)
        {
            homePosition = position;
        }

        public bool PickNewWanderDestination()
        {
            CacheReferences();
            if (cachedNavigator == null || !cachedNavigator.TryBindToNavMesh())
            {
                return false;
            }

            int areaMask = cachedNavigator.Agent != null
                ? cachedNavigator.Agent.areaMask
                : NavMesh.AllAreas;

            for (int attempt = 0; attempt < wanderSampleAttempts; attempt++)
            {
                Vector2 randomOffset = UnityEngine.Random.insideUnitCircle * wanderRadius;
                Vector3 candidate = homePosition + new Vector3(randomOffset.x, 0f, randomOffset.y);

                if (!NavMesh.SamplePosition(
                        candidate,
                        out NavMeshHit hit,
                        wanderSampleDistance,
                        areaMask))
                {
                    continue;
                }

                if (cachedNavigator.SetDestination(hit.position))
                {
                    waitingForWanderDestination = false;
                    return true;
                }
            }

            return false;
        }

        private void UpdateWander()
        {
            if (cachedNavigator == null)
            {
                return;
            }

            if (cachedNavigator.HasDestination && !cachedNavigator.HasReachedDestination)
            {
                waitingForWanderDestination = false;
                return;
            }

            if (!waitingForWanderDestination)
            {
                waitingForWanderDestination = true;
                nextWanderTime = Time.time + UnityEngine.Random.Range(
                    minimumWanderWait,
                    maximumWanderWait);
            }

            if (Time.time < nextWanderTime)
            {
                return;
            }

            if (!PickNewWanderDestination())
            {
                nextWanderTime = Time.time + 1f;
            }
        }

        private void UpdateChase()
        {
            if (chaseTarget == null || !chaseTarget.gameObject.activeInHierarchy)
            {
                EndTimedChase();
                return;
            }

            if (!float.IsPositiveInfinity(chaseEndTime) && Time.time >= chaseEndTime)
            {
                EndTimedChase();
                return;
            }

            if (cachedNavigator != null && cachedNavigator.Target != chaseTarget)
            {
                cachedNavigator.SetTarget(chaseTarget);
            }
        }

        private void EndTimedChase()
        {
            chaseTarget = null;
            chaseEndTime = float.PositiveInfinity;

            ApexNPCBehaviorMode returnMode = modeAfterImpactChase == ApexNPCBehaviorMode.Chase
                ? ApexNPCBehaviorMode.Wander
                : modeAfterImpactChase;

            SetMode(returnMode);
        }

        private void HandleImpact(ApexImpactInfo impact)
        {
            if (!chaseWhenHit || impact.OtherRigidbody == null)
            {
                return;
            }

            bool hardEnough = impact.Speed >= minimumImpactSpeed ||
                              impact.Impulse >= minimumImpactImpulse;
            if (!hardEnough)
            {
                return;
            }

            Transform target = ResolveImpactTarget(impact.OtherRigidbody);
            if (target == null || target == transform || target.IsChildOf(transform))
            {
                return;
            }

            ImpactTargetDetected?.Invoke(target);
            BeginTimedChase(target, impactChaseDuration);
        }

        private Transform ResolveImpactTarget(Rigidbody otherRigidbody)
        {
            ApexBody otherBody = otherRigidbody.GetComponentInParent<ApexBody>();
            return otherBody != null ? otherBody.transform : otherRigidbody.transform;
        }

        private void CacheReferences()
        {
            if (cachedBody == null)
            {
                cachedBody = GetComponent<ApexBody>();
            }

            if (cachedNavigator == null)
            {
                cachedNavigator = GetComponent<ApexNPCNavigator>();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            wanderRadius = Mathf.Max(0.1f, wanderRadius);
            wanderSampleDistance = Mathf.Max(0.1f, wanderSampleDistance);
            wanderSampleAttempts = Mathf.Max(1, wanderSampleAttempts);
            minimumWanderWait = Mathf.Max(0f, minimumWanderWait);
            maximumWanderWait = Mathf.Max(minimumWanderWait, maximumWanderWait);
            minimumImpactSpeed = Mathf.Max(0f, minimumImpactSpeed);
            minimumImpactImpulse = Mathf.Max(0f, minimumImpactImpulse);
            impactChaseDuration = Mathf.Max(0f, impactChaseDuration);
            CacheReferences();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.DrawWireSphere(
                Application.isPlaying ? homePosition : transform.position,
                wanderRadius);
        }
#endif
    }
}
