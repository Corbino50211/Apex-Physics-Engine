using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Reapplies persistent ragdoll collision exclusions at runtime.
    /// Connected bones are ignored by default; all self-collisions can optionally be disabled.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ApexRagdollCollisionFilter : MonoBehaviour
    {
        [SerializeField] private Collider[] ragdollColliders = System.Array.Empty<Collider>();
        [SerializeField] private ConfigurableJoint[] ragdollJoints = System.Array.Empty<ConfigurableJoint>();
        [SerializeField] private bool ignoreConnectedBones = true;
        [SerializeField] private bool ignoreAllSelfCollisions;

        public bool IgnoreAllSelfCollisions => ignoreAllSelfCollisions;

        public void Configure(
            Collider[] colliders,
            ConfigurableJoint[] joints,
            bool shouldIgnoreAllSelfCollisions = false)
        {
            ragdollColliders = colliders ?? System.Array.Empty<Collider>();
            ragdollJoints = joints ?? System.Array.Empty<ConfigurableJoint>();
            ignoreConnectedBones = true;
            ignoreAllSelfCollisions = shouldIgnoreAllSelfCollisions;
            Apply();
        }

        public void SetIgnoreAllSelfCollisions(bool shouldIgnore)
        {
            ignoreAllSelfCollisions = shouldIgnore;
            Apply();
        }

        public void RefreshFromChildren()
        {
            ragdollColliders = GetComponentsInChildren<Collider>(true);
            ragdollJoints = GetComponentsInChildren<ConfigurableJoint>(true);
            Apply();
        }

        public void Apply()
        {
            if (ignoreAllSelfCollisions)
            {
                for (int i = 0; i < ragdollColliders.Length; i++)
                {
                    Collider first = ragdollColliders[i];
                    if (first == null)
                    {
                        continue;
                    }

                    for (int j = i + 1; j < ragdollColliders.Length; j++)
                    {
                        Collider second = ragdollColliders[j];
                        if (second != null && first != second)
                        {
                            Physics.IgnoreCollision(first, second, true);
                        }
                    }
                }

                return;
            }

            if (!ignoreConnectedBones)
            {
                return;
            }

            for (int i = 0; i < ragdollJoints.Length; i++)
            {
                ConfigurableJoint joint = ragdollJoints[i];
                if (joint == null || joint.connectedBody == null)
                {
                    continue;
                }

                Collider[] childColliders = joint.GetComponents<Collider>();
                Collider[] parentColliders = joint.connectedBody.GetComponents<Collider>();
                for (int childIndex = 0; childIndex < childColliders.Length; childIndex++)
                {
                    Collider child = childColliders[childIndex];
                    if (child == null)
                    {
                        continue;
                    }

                    for (int parentIndex = 0; parentIndex < parentColliders.Length; parentIndex++)
                    {
                        Collider parent = parentColliders[parentIndex];
                        if (parent != null)
                        {
                            Physics.IgnoreCollision(child, parent, true);
                        }
                    }
                }
            }
        }

        private void OnEnable()
        {
            Apply();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying && (ragdollColliders == null || ragdollColliders.Length == 0))
            {
                ragdollColliders = GetComponentsInChildren<Collider>(true);
                ragdollJoints = GetComponentsInChildren<ConfigurableJoint>(true);
            }
        }
#endif
    }
}
