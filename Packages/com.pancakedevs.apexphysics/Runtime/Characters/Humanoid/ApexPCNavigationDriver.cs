using UnityEngine;
using UnityEngine.AI;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Bridges an older Humanoid-root navigator to the authoritative Apex PC motor.
    /// It keeps NavMesh planning projected beneath the motor, wakes the motor when a
    /// destination exists, and retries paths that become invalid, partial, or stuck.
    /// </summary>
    [DefaultExecutionOrder(-260)]
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class ApexPCNavigationDriver : MonoBehaviour
    {
        [SerializeField] private ApexPCPhysicalCharacter character;
        [SerializeField] private ApexNPCNavigator navigator;

        [Header("Recovery")]
        [SerializeField, Min(0.1f)] private float stuckCheckDelay = 1.25f;
        [SerializeField, Min(0f)] private float minimumProgressDistance = 0.08f;
        [SerializeField, Min(0.05f)] private float failedPathRetryDelay = 0.4f;

        private Rigidbody motorBody;
        private Vector3 progressPosition;
        private float progressCheckedAt;
        private float nextPathRetryTime;
        private bool subscribed;

        public ApexNPCNavigator Navigator => navigator;

        private void Awake()
        {
            Configure(character != null ? character : GetComponent<ApexPCPhysicalCharacter>());
        }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            if (!ResolveReferences() || !IsCharacterControllable())
            {
                ResetProgressTracking();
                return;
            }

            navigator.ConfigureMovementRoot(motorBody.transform);
            ConfigureAgent();

            if (!navigator.HasDestination)
            {
                ResetProgressTracking();
                return;
            }

            motorBody.WakeUp();

            if (!navigator.IsOnNavMesh)
            {
                RetryPathWhenAllowed();
                return;
            }

            if (!navigator.IsPathPending &&
                (!navigator.HasPath || navigator.PathStatus != NavMeshPathStatus.PathComplete))
            {
                RetryPathWhenAllowed();
                return;
            }

            if (navigator.HasReachedDestination)
            {
                ResetProgressTracking();
                return;
            }

            if (Time.time - progressCheckedAt < stuckCheckDelay)
            {
                return;
            }

            float progress = Vector3.ProjectOnPlane(
                motorBody.position - progressPosition,
                Vector3.up).magnitude;
            bool askingToMove = navigator.DesiredVelocity.sqrMagnitude > 0.0025f ||
                                navigator.RemainingDistance > 0.25f;

            progressPosition = motorBody.position;
            progressCheckedAt = Time.time;

            if (askingToMove && progress < minimumProgressDistance)
            {
                navigator.TryBindToNavMesh();
                navigator.ForceRepath();
                motorBody.WakeUp();
            }
        }

        public void Configure(ApexPCPhysicalCharacter owner)
        {
            Unsubscribe();
            character = owner;
            navigator = owner != null && owner.Humanoid != null
                ? owner.Humanoid.NPCNavigator
                : null;
            motorBody = owner != null ? owner.MotorBody : null;

            if (navigator != null && motorBody != null)
            {
                navigator.ConfigureMovementRoot(motorBody.transform);
                ConfigureAgent();
            }

            ResetProgressTracking();
            Subscribe();
        }

        private bool ResolveReferences()
        {
            if (character == null)
            {
                character = GetComponent<ApexPCPhysicalCharacter>();
            }

            if (character == null || character.Humanoid == null)
            {
                return false;
            }

            if (navigator == null)
            {
                navigator = character.Humanoid.NPCNavigator;
            }

            if (motorBody == null)
            {
                motorBody = character.MotorBody;
            }

            return navigator != null && motorBody != null;
        }

        private void ConfigureAgent()
        {
            NavMeshAgent agent = navigator != null ? navigator.Agent : null;
            if (agent == null || character == null)
            {
                return;
            }

            CapsuleCollider motorCollider = character.MotorCollider;
            if (motorCollider != null)
            {
                agent.radius = Mathf.Max(0.05f, motorCollider.radius * 0.85f);
                agent.height = Mathf.Max(agent.radius * 2f, motorCollider.height);
            }

            agent.baseOffset = 0f;
            agent.updatePosition = false;
            agent.updateRotation = false;
            agent.updateUpAxis = false;
            agent.autoRepath = true;
        }

        private void RetryPathWhenAllowed()
        {
            if (Time.time < nextPathRetryTime)
            {
                return;
            }

            nextPathRetryTime = Time.time + failedPathRetryDelay;
            navigator.TryBindToNavMesh();
            navigator.ForceRepath();
            motorBody.WakeUp();
        }

        private bool IsCharacterControllable()
        {
            return character.State == ApexPCCharacterState.Active ||
                   character.State == ApexPCCharacterState.Staggered;
        }

        private void ResetProgressTracking()
        {
            if (motorBody != null)
            {
                progressPosition = motorBody.position;
            }
            progressCheckedAt = Time.time;
        }

        private void HandleCharacterStateChanged(ApexPCCharacterState state)
        {
            ResetProgressTracking();
            if (state == ApexPCCharacterState.Active && navigator != null && motorBody != null)
            {
                navigator.ConfigureMovementRoot(motorBody.transform);
                navigator.TryBindToNavMesh();
                if (navigator.HasDestination)
                {
                    navigator.ForceRepath();
                }
            }
        }

        private void Subscribe()
        {
            if (subscribed || character == null)
            {
                return;
            }

            character.StateChanged += HandleCharacterStateChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || character == null)
            {
                subscribed = false;
                return;
            }

            character.StateChanged -= HandleCharacterStateChanged;
            subscribed = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            stuckCheckDelay = Mathf.Max(0.1f, stuckCheckDelay);
            minimumProgressDistance = Mathf.Max(0f, minimumProgressDistance);
            failedPathRetryDelay = Mathf.Max(0.05f, failedPathRetryDelay);
        }
#endif
    }
}
