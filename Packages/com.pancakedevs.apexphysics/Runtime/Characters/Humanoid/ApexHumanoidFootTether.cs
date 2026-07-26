using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Legacy soft constraint between a physical humanoid foot and a locoball anchor.
    /// The unified physical humanoid controller disables this component because active
    /// locomotion is now driven by the animated target skeleton instead.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ApexHumanoidFootTether : MonoBehaviour
    {
        [SerializeField] private SpringJoint tetherJoint;
        [SerializeField] private Rigidbody supportBody;
        [SerializeField] private Transform locoballAnchor;
        [SerializeField] private ApexActiveRagdoll activeRagdoll;

        [Header("Active Tether")]
        [SerializeField, Min(0f)] private float spring = 1800f;
        [SerializeField, Min(0f)] private float damper = 120f;
        [SerializeField, Min(0f)] private float maximumDistance = 0.28f;
        [SerializeField, Min(0f)] private float tolerance = 0.025f;

        [Header("Limp Tether")]
        [SerializeField, Min(0f)] private float limpMaximumDistance = 2f;

        private bool subscribed;
        private bool tetherActive = true;

        public SpringJoint Joint => tetherJoint;
        public Transform LocoballAnchor => locoballAnchor;
        public bool TetherActive => tetherActive;

        private void OnEnable()
        {
            Subscribe();
            ApplyCurrentState();
        }

        private void OnDisable()
        {
            Unsubscribe();
            SetTetherActive(false);
        }

        public void Configure(
            Rigidbody newSupportBody,
            Transform newAnchor,
            ApexActiveRagdoll newActiveRagdoll,
            float newSpring,
            float newDamper,
            float newMaximumDistance,
            float newLimpMaximumDistance)
        {
            Unsubscribe();

            supportBody = newSupportBody;
            locoballAnchor = newAnchor;
            activeRagdoll = newActiveRagdoll;
            spring = Mathf.Max(0f, newSpring);
            damper = Mathf.Max(0f, newDamper);
            maximumDistance = Mathf.Max(0f, newMaximumDistance);
            limpMaximumDistance = Mathf.Max(maximumDistance, newLimpMaximumDistance);
            tetherActive = true;

            EnsureJoint();
            RefreshAnchor();
            Subscribe();
            ApplyCurrentState();
        }

        public void SetTetherActive(bool active)
        {
            tetherActive = active;
            EnsureJoint();
            if (tetherJoint == null)
            {
                return;
            }

            if (!active)
            {
                tetherJoint.spring = 0f;
                tetherJoint.damper = 0f;
                tetherJoint.minDistance = 0f;
                tetherJoint.maxDistance = Mathf.Max(limpMaximumDistance, 100f);
            }
            else
            {
                RefreshAnchor();
                ApplyCurrentState();
            }
        }

        public void RefreshAnchor()
        {
            EnsureJoint();
            if (tetherJoint == null || supportBody == null || locoballAnchor == null)
            {
                return;
            }

            tetherJoint.connectedBody = supportBody;
            tetherJoint.autoConfigureConnectedAnchor = false;
            tetherJoint.anchor = Vector3.zero;
            tetherJoint.connectedAnchor = supportBody.transform.InverseTransformPoint(locoballAnchor.position);
            tetherJoint.enableCollision = false;
            tetherJoint.tolerance = tolerance;
        }

        private void EnsureJoint()
        {
            if (tetherJoint != null)
            {
                return;
            }

            SpringJoint[] existing = GetComponents<SpringJoint>();
            for (int i = 0; i < existing.Length; i++)
            {
                SpringJoint candidate = existing[i];
                if (candidate != null &&
                    (candidate.connectedBody == supportBody || candidate.connectedBody == null))
                {
                    tetherJoint = candidate;
                    break;
                }
            }

            if (tetherJoint == null && tetherActive)
            {
                tetherJoint = gameObject.AddComponent<SpringJoint>();
            }
        }

        private void Subscribe()
        {
            if (subscribed || activeRagdoll == null)
            {
                return;
            }

            activeRagdoll.StateChanged += HandleRagdollStateChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || activeRagdoll == null)
            {
                return;
            }

            activeRagdoll.StateChanged -= HandleRagdollStateChanged;
            subscribed = false;
        }

        private void HandleRagdollStateChanged(ApexRagdollState state)
        {
            ApplyState(state);
        }

        private void ApplyCurrentState()
        {
            ApplyState(activeRagdoll != null ? activeRagdoll.State : ApexRagdollState.Active);
        }

        private void ApplyState(ApexRagdollState state)
        {
            EnsureJoint();
            if (tetherJoint == null || !tetherActive)
            {
                return;
            }

            bool limp = state == ApexRagdollState.Limp;
            tetherJoint.spring = limp ? 0f : spring;
            tetherJoint.damper = limp ? 0f : damper;
            tetherJoint.minDistance = 0f;
            tetherJoint.maxDistance = limp ? limpMaximumDistance : maximumDistance;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            spring = Mathf.Max(0f, spring);
            damper = Mathf.Max(0f, damper);
            maximumDistance = Mathf.Max(0f, maximumDistance);
            tolerance = Mathf.Max(0f, tolerance);
            limpMaximumDistance = Mathf.Max(maximumDistance, limpMaximumDistance);
        }
#endif
    }
}
