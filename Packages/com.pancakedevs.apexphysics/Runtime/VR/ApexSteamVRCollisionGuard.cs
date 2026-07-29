using System.Collections.Generic;
using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Keeps Valve's Interaction System Player authoritative for SteamVR tracking.
    /// A dynamic Rigidbody parent around Valve's tracked-space prefab creates a feedback
    /// loop: tracked motion changes the capsule, physics moves the parent, and the tracked
    /// camera/hands are displaced again. That can launch the rig and make the controllers
    /// flicker or vibrate continuously.
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

            // Valve owns the SteamVR tracking origin. Apex Rigidbody locomotion is disabled
            // until a backend-specific movement bridge can move Valve's Player safely.
            ApexVRPlayerMotor motor = rig.GetComponent<ApexVRPlayerMotor>();
            if (motor != null)
            {
                motor.ClearInput();
                motor.enabled = false;
            }

            ApexVRInput input = rig.GetComponent<ApexVRInput>();
            if (input != null)
            {
                input.enabled = false;
            }

            CapsuleCollider bodyCollider = rig.GetComponent<CapsuleCollider>();
            if (bodyCollider != null)
            {
                bodyCollider.enabled = false;
            }

            Rigidbody body = rig.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.useGravity = false;
                body.isKinematic = true;
            }

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

            Rigidbody proxyBody = apexHand.GetComponent<Rigidbody>();
            if (proxyBody != null)
            {
                proxyBody.linearVelocity = Vector3.zero;
                proxyBody.angularVelocity = Vector3.zero;
                proxyBody.useGravity = false;
                proxyBody.isKinematic = true;
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
