using System.Collections.Generic;
using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Prevents Valve Interaction System hand colliders, Apex hand proxies, and the
    /// Apex player capsule from colliding with each other. SteamVR already owns its
    /// tracked-hand contact model, so duplicate self-collisions create feedback,
    /// rig jitter, and continuous controller haptics.
    /// </summary>
    internal static class ApexSteamVRCollisionGuard
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplyToLoadedScene()
        {
            ApexVRPlayerRig[] rigs = Object.FindObjectsByType<ApexVRPlayerRig>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < rigs.Length; i++)
            {
                Apply(rigs[i]);
            }
        }

        private static void Apply(ApexVRPlayerRig rig)
        {
            if (rig == null || rig.Backend != ApexVRBackend.SteamVROpenVR)
            {
                return;
            }

            Collider bodyCollider = rig.GetComponent<Collider>();
            List<Collider> valveHandColliders = new List<Collider>();

            CollectAndDisableApexProxy(rig.LeftHand, bodyCollider, valveHandColliders);
            CollectAndDisableApexProxy(rig.RightHand, bodyCollider, valveHandColliders);

            for (int i = 0; i < valveHandColliders.Count; i++)
            {
                Collider first = valveHandColliders[i];
                if (first == null)
                {
                    continue;
                }

                if (bodyCollider != null && first != bodyCollider)
                {
                    Physics.IgnoreCollision(first, bodyCollider, true);
                }

                for (int j = i + 1; j < valveHandColliders.Count; j++)
                {
                    Collider second = valveHandColliders[j];
                    if (second != null && second != first)
                    {
                        Physics.IgnoreCollision(first, second, true);
                    }
                }
            }
        }

        private static void CollectAndDisableApexProxy(
            ApexVRPhysicalHand apexHand,
            Collider bodyCollider,
            List<Collider> valveHandColliders)
        {
            if (apexHand == null)
            {
                return;
            }

            Collider apexProxyCollider = apexHand.GetComponent<Collider>();
            if (apexProxyCollider != null)
            {
                if (bodyCollider != null && apexProxyCollider != bodyCollider)
                {
                    Physics.IgnoreCollision(apexProxyCollider, bodyCollider, true);
                }

                apexProxyCollider.enabled = false;
            }

            Transform valveHand = apexHand.TrackingTarget;
            if (valveHand == null)
            {
                return;
            }

            Collider[] colliders = valveHand.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider != null && !valveHandColliders.Contains(collider))
                {
                    valveHandColliders.Add(collider);
                }
            }
        }
    }
}
