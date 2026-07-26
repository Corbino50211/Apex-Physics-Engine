using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Relays hard impacts detected on any active-ragdoll bone into the NPC brain's timed chase behavior.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ApexHumanoidNPCImpactRelay : MonoBehaviour
    {
        [SerializeField] private ApexActiveRagdoll activeRagdoll;
        [SerializeField] private ApexNPCBrain npcBrain;
        [SerializeField, Min(0f)] private float chaseDuration = 10f;

        private bool subscribed;

        public void Configure(
            ApexActiveRagdoll ragdoll,
            ApexNPCBrain brain,
            float duration = 10f)
        {
            Unsubscribe();
            activeRagdoll = ragdoll;
            npcBrain = brain;
            chaseDuration = Mathf.Max(0f, duration);
            Subscribe();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (subscribed || activeRagdoll == null)
            {
                return;
            }

            activeRagdoll.KnockedDown += HandleKnockedDown;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || activeRagdoll == null)
            {
                subscribed = false;
                return;
            }

            activeRagdoll.KnockedDown -= HandleKnockedDown;
            subscribed = false;
        }

        private void HandleKnockedDown(ApexRagdollBone bone, Collision collision)
        {
            if (npcBrain == null || collision == null || collision.rigidbody == null)
            {
                return;
            }

            ApexBody otherApexBody = collision.rigidbody.GetComponentInParent<ApexBody>();
            Transform target = otherApexBody != null
                ? otherApexBody.transform
                : collision.rigidbody.transform;
            if (target == null || target == transform || target.IsChildOf(transform.root))
            {
                return;
            }

            npcBrain.BeginTimedChase(target, chaseDuration);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            chaseDuration = Mathf.Max(0f, chaseDuration);
        }
#endif
    }
}
