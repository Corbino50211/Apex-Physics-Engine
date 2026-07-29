using System.Collections.Generic;
using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Adds a safety layer around physical VR grabbing. It prevents the first physics
    /// frame of a grab from launching light objects, disables hand/object self-collision
    /// while held, and caps runaway velocity without making the held object kinematic.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ApexGrabber))]
    public sealed class ApexVRGrabStabilizer : MonoBehaviour
    {
        [Header("Grab Entry")]
        [SerializeField, Min(0f)] private float maximumInitialSnapDistance = 0.25f;
        [SerializeField, Min(0f)] private float maximumInitialLinearSpeed = 1.5f;
        [SerializeField, Min(0f)] private float maximumInitialAngularSpeed = 4f;

        [Header("Held Limits")]
        [SerializeField, Min(0f)] private float maximumHeldLinearSpeed = 8f;
        [SerializeField, Min(0f)] private float maximumHeldAngularSpeed = 16f;

        private readonly List<ColliderPair> ignoredPairs = new List<ColliderPair>(16);
        private ApexGrabber grabber;
        private ApexGrabbable heldObject;
        private Rigidbody heldBody;

        private readonly struct ColliderPair
        {
            public ColliderPair(Collider first, Collider second)
            {
                First = first;
                Second = second;
            }

            public Collider First { get; }
            public Collider Second { get; }
        }

        private void Awake()
        {
            grabber = GetComponent<ApexGrabber>();
        }

        private void OnEnable()
        {
            if (grabber == null)
            {
                grabber = GetComponent<ApexGrabber>();
            }

            if (grabber != null)
            {
                grabber.Grabbed += HandleGrabbed;
                grabber.Released += HandleReleased;
            }
        }

        private void OnDisable()
        {
            if (grabber != null)
            {
                grabber.Grabbed -= HandleGrabbed;
                grabber.Released -= HandleReleased;
            }

            RestoreIgnoredCollisions();
            heldObject = null;
            heldBody = null;
        }

        private void FixedUpdate()
        {
            if (heldBody == null || heldBody.isKinematic)
            {
                return;
            }

            if (maximumHeldLinearSpeed > 0f)
            {
                heldBody.linearVelocity = Vector3.ClampMagnitude(
                    heldBody.linearVelocity,
                    maximumHeldLinearSpeed);
            }

            if (maximumHeldAngularSpeed > 0f)
            {
                heldBody.angularVelocity = Vector3.ClampMagnitude(
                    heldBody.angularVelocity,
                    maximumHeldAngularSpeed);
            }
        }

        private void HandleGrabbed(ApexGrabbable grabbable)
        {
            RestoreIgnoredCollisions();

            heldObject = grabbable;
            heldBody = heldObject != null && heldObject.Body != null
                ? heldObject.Body.Rigidbody
                : null;

            if (heldObject == null || heldBody == null || heldBody.isKinematic || grabber == null)
            {
                return;
            }

            if (heldObject.TryGetBestGrabPose(
                    grabber.GripTarget.position,
                    grabber.GripTarget.rotation,
                    grabber.Handedness,
                    out ApexGrabPose pose))
            {
                Vector3 gripPosition = heldObject.GetWorldGripPosition(pose);
                Vector3 correction = grabber.GripTarget.position - gripPosition;
                if (maximumInitialSnapDistance > 0f)
                {
                    correction = Vector3.ClampMagnitude(correction, maximumInitialSnapDistance);
                }

                heldBody.position += correction;
            }

            heldBody.linearVelocity = Vector3.ClampMagnitude(
                heldBody.linearVelocity,
                maximumInitialLinearSpeed);
            heldBody.angularVelocity = Vector3.ClampMagnitude(
                heldBody.angularVelocity,
                maximumInitialAngularSpeed);
            heldBody.WakeUp();

            IgnoreHandObjectCollisions();
        }

        private void HandleReleased(ApexGrabbable grabbable)
        {
            RestoreIgnoredCollisions();
            heldObject = null;
            heldBody = null;
        }

        private void IgnoreHandObjectCollisions()
        {
            if (heldObject == null)
            {
                return;
            }

            Collider[] handColliders = GetComponentsInChildren<Collider>(true);
            Collider[] objectColliders = heldObject.GetComponentsInChildren<Collider>(true);

            for (int i = 0; i < handColliders.Length; i++)
            {
                Collider handCollider = handColliders[i];
                if (handCollider == null || !handCollider.enabled)
                {
                    continue;
                }

                for (int j = 0; j < objectColliders.Length; j++)
                {
                    Collider objectCollider = objectColliders[j];
                    if (objectCollider == null || !objectCollider.enabled || objectCollider == handCollider)
                    {
                        continue;
                    }

                    Physics.IgnoreCollision(handCollider, objectCollider, true);
                    ignoredPairs.Add(new ColliderPair(handCollider, objectCollider));
                }
            }
        }

        private void RestoreIgnoredCollisions()
        {
            for (int i = 0; i < ignoredPairs.Count; i++)
            {
                ColliderPair pair = ignoredPairs[i];
                if (pair.First != null && pair.Second != null)
                {
                    Physics.IgnoreCollision(pair.First, pair.Second, false);
                }
            }

            ignoredPairs.Clear();
        }

        private void OnValidate()
        {
            maximumInitialSnapDistance = Mathf.Max(0f, maximumInitialSnapDistance);
            maximumInitialLinearSpeed = Mathf.Max(0f, maximumInitialLinearSpeed);
            maximumInitialAngularSpeed = Mathf.Max(0f, maximumInitialAngularSpeed);
            maximumHeldLinearSpeed = Mathf.Max(0f, maximumHeldLinearSpeed);
            maximumHeldAngularSpeed = Mathf.Max(0f, maximumHeldAngularSpeed);
        }
    }

    /// <summary>
    /// Automatically installs the VR grab stabilizer on physical OpenXR hands in
    /// existing scenes, so users do not need to rebuild their rig after updating.
    /// </summary>
    internal static class ApexVRGrabStabilizerBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            ApexPhysicalOpenXRHand[] hands = Object.FindObjectsByType<ApexPhysicalOpenXRHand>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < hands.Length; i++)
            {
                ApexPhysicalOpenXRHand hand = hands[i];
                if (hand == null)
                {
                    continue;
                }

                ApexGrabber grabber = hand.GetComponent<ApexGrabber>();
                if (grabber != null && hand.GetComponent<ApexVRGrabStabilizer>() == null)
                {
                    hand.gameObject.AddComponent<ApexVRGrabStabilizer>();
                }
            }
        }
    }
}
