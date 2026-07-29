using System;
using UnityEngine;
using UnityEngine.AI;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Path-planning layer for an Apex NPC. The NavMeshAgent calculates routes and
    /// avoidance, but does not directly move or rotate the NPC transform. A separate
    /// movement root can be assigned when the physical locomotion body does not live on
    /// the same GameObject as this navigator.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class ApexNPCNavigator : MonoBehaviour
    {
        [Header("Destination")]
        [SerializeField] private Transform target;
        [SerializeField] private bool followTarget = true;
        [SerializeField] private bool navigateOnStart = true;
        [SerializeField, Min(0f)] private float stoppingDistance = 0.75f;

        [Header("Path Updates")]
        [SerializeField, Min(0.02f)] private float repathInterval = 0.25f;
        [SerializeField, Min(0.01f)] private float navMeshSampleDistance = 2f;
        [SerializeField] private bool keepAgentSyncedToTransform = true;

        [Header("External Movement Root")]
        [Tooltip("Optional authoritative locomotion transform. Apex PC characters assign their motor here.")]
        [SerializeField] private Transform movementRoot;
        [SerializeField] private bool projectMovementRootToNavMesh = true;

        private NavMeshAgent cachedAgent;
        private Vector3 destination;
        private bool hasDestination;
        private bool destinationReached;
        private float nextRepathTime;

        public event Action<ApexNPCNavigator> PathUpdated;
        public event Action<ApexNPCNavigator> DestinationReached;
        public event Action<ApexNPCNavigator> PathFailed;

        public NavMeshAgent Agent
        {
            get
            {
                CacheAgent();
                return cachedAgent;
            }
        }

        public Transform Target => target;
        public Transform MovementRoot => movementRoot;
        public bool HasDestination => hasDestination;
        public Vector3 Destination => destination;
        public Vector3 NavigationPosition => ResolveNavigationPosition();
        public bool IsOnNavMesh => CanUseAgent() && cachedAgent.isOnNavMesh;
        public bool HasPath => IsOnNavMesh && cachedAgent.hasPath;
        public bool IsPathPending => IsOnNavMesh && cachedAgent.pathPending;
        public NavMeshPathStatus PathStatus => IsOnNavMesh
            ? cachedAgent.pathStatus
            : NavMeshPathStatus.PathInvalid;
        public bool HasCompletePath => HasPath && !IsPathPending &&
                                       PathStatus == NavMeshPathStatus.PathComplete;
        public Vector3 DesiredVelocity => IsOnNavMesh
            ? cachedAgent.desiredVelocity
            : Vector3.zero;
        public Vector3 SteeringTarget => HasPath
            ? cachedAgent.steeringTarget
            : NavigationPosition;
        public float RemainingDistance => hasDestination
            ? Vector3.Distance(NavigationPosition, destination)
            : 0f;
        public bool HasReachedDestination => destinationReached;

        private void Reset()
        {
            CacheAgent();
            ConfigureAgent();
        }

        private void Awake()
        {
            CacheAgent();
            ConfigureAgent();
        }

        private void OnEnable()
        {
            nextRepathTime = Time.time;
            TryBindToNavMesh();
        }

        private void Start()
        {
            if (navigateOnStart && target != null)
            {
                SetTarget(target);
            }
        }

        private void Update()
        {
            if (!CanUseAgent())
            {
                return;
            }

            if (!cachedAgent.isOnNavMesh && !TryBindToNavMesh())
            {
                return;
            }

            if (followTarget && target != null && Time.time >= nextRepathTime)
            {
                SetDestination(target.position);
            }

            EvaluateArrival();
        }

        private void LateUpdate()
        {
            if (!keepAgentSyncedToTransform || !IsOnNavMesh)
            {
                return;
            }

            if (TryResolveProjectedNavigationPosition(out Vector3 navigationPosition))
            {
                cachedAgent.nextPosition = navigationPosition;
            }
        }

        public void ConfigureMovementRoot(Transform newMovementRoot)
        {
            movementRoot = newMovementRoot;
            nextRepathTime = Time.time;

            if (!isActiveAndEnabled)
            {
                return;
            }

            TryBindToNavMesh();
            if (hasDestination)
            {
                ForceRepath();
            }
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;

            if (target == null)
            {
                ClearDestination();
                return;
            }

            SetDestination(target.position);
        }

        public bool SetDestination(Vector3 worldDestination)
        {
            CacheAgent();
            nextRepathTime = Time.time + repathInterval;

            if (!CanUseAgent() || (!cachedAgent.isOnNavMesh && !TryBindToNavMesh()))
            {
                PathFailed?.Invoke(this);
                return false;
            }

            if (!NavMesh.SamplePosition(
                    worldDestination,
                    out NavMeshHit hit,
                    navMeshSampleDistance,
                    cachedAgent.areaMask))
            {
                PathFailed?.Invoke(this);
                return false;
            }

            destination = hit.position;
            hasDestination = true;
            destinationReached = false;
            cachedAgent.stoppingDistance = stoppingDistance;

            bool accepted = cachedAgent.SetDestination(destination);
            if (accepted)
            {
                PathUpdated?.Invoke(this);
            }
            else
            {
                PathFailed?.Invoke(this);
            }

            return accepted;
        }

        public bool ForceRepath()
        {
            if (!hasDestination)
            {
                return false;
            }

            Vector3 savedDestination = destination;
            if (!TryBindToNavMesh())
            {
                PathFailed?.Invoke(this);
                return false;
            }

            return SetDestination(savedDestination);
        }

        public void ClearDestination()
        {
            hasDestination = false;
            destinationReached = false;

            if (IsOnNavMesh)
            {
                cachedAgent.ResetPath();
            }
        }

        public bool TryBindToNavMesh()
        {
            CacheAgent();
            if (!CanUseAgent())
            {
                return false;
            }

            if (!TryResolveProjectedNavigationPosition(out Vector3 navigationPosition))
            {
                return false;
            }

            if (cachedAgent.isOnNavMesh)
            {
                cachedAgent.nextPosition = navigationPosition;
                return true;
            }

            bool warped = cachedAgent.Warp(navigationPosition);
            if (warped && keepAgentSyncedToTransform)
            {
                cachedAgent.nextPosition = navigationPosition;
            }

            return warped;
        }

        private void EvaluateArrival()
        {
            if (!hasDestination || destinationReached || cachedAgent.pathPending)
            {
                return;
            }

            float arrivalDistance = Mathf.Max(stoppingDistance, 0.01f);
            Vector3 difference = NavigationPosition - destination;
            difference.y = 0f;
            if (difference.sqrMagnitude > arrivalDistance * arrivalDistance)
            {
                return;
            }

            destinationReached = true;
            DestinationReached?.Invoke(this);
        }

        private Vector3 ResolveNavigationPosition()
        {
            if (TryResolveProjectedNavigationPosition(out Vector3 projected))
            {
                return projected;
            }

            return movementRoot != null ? movementRoot.position : transform.position;
        }

        private bool TryResolveProjectedNavigationPosition(out Vector3 position)
        {
            Vector3 source = movementRoot != null ? movementRoot.position : transform.position;
            if (!projectMovementRootToNavMesh)
            {
                position = source;
                return true;
            }

            int areaMask = cachedAgent != null ? cachedAgent.areaMask : NavMesh.AllAreas;
            if (NavMesh.SamplePosition(source, out NavMeshHit hit, navMeshSampleDistance, areaMask))
            {
                position = hit.position;
                return true;
            }

            position = source;
            return false;
        }

        private void ConfigureAgent()
        {
            if (cachedAgent == null)
            {
                return;
            }

            cachedAgent.updatePosition = false;
            cachedAgent.updateRotation = false;
            cachedAgent.updateUpAxis = false;
            cachedAgent.stoppingDistance = stoppingDistance;
            cachedAgent.autoRepath = true;
        }

        private bool CanUseAgent()
        {
            return cachedAgent != null &&
                   cachedAgent.enabled &&
                   cachedAgent.gameObject.activeInHierarchy;
        }

        private void CacheAgent()
        {
            if (cachedAgent == null)
            {
                cachedAgent = GetComponent<NavMeshAgent>();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            stoppingDistance = Mathf.Max(0f, stoppingDistance);
            repathInterval = Mathf.Max(0.02f, repathInterval);
            navMeshSampleDistance = Mathf.Max(0.01f, navMeshSampleDistance);
            CacheAgent();
            ConfigureAgent();
        }

        private void OnDrawGizmosSelected()
        {
            if (hasDestination || target != null)
            {
                Vector3 point = target != null ? target.position : destination;
                Gizmos.DrawWireSphere(point, Mathf.Max(stoppingDistance, 0.05f));
                Gizmos.DrawLine(
                    movementRoot != null ? movementRoot.position : transform.position,
                    point);
            }
        }
#endif
    }
}
