using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Desktop interaction layer for the Apex PC player rig. It raycasts from the
    /// camera, highlights the current grabbable, adjusts the force-driven hold point,
    /// throws held objects, and temporarily ignores player collisions while holding.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ApexPCPlayerInteraction : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera viewCamera;
        [SerializeField] private ApexGrabber grabber;
        [SerializeField] private Transform holdTarget;
        [SerializeField] private Collider[] playerColliders = System.Array.Empty<Collider>();

        [Header("Detection")]
        [SerializeField, Min(0.1f)] private float interactionDistance = 3f;
        [SerializeField] private LayerMask interactionLayers = ~0;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        [Header("Hold Distance")]
        [SerializeField, Min(0.1f)] private float holdDistance = 1.25f;
        [SerializeField, Min(0.1f)] private float minimumHoldDistance = 0.65f;
        [SerializeField, Min(0.1f)] private float maximumHoldDistance = 3f;
        [SerializeField, Min(0.01f)] private float scrollSensitivity = 0.2f;
        [SerializeField] private Vector2 holdOffset = new Vector2(0f, -0.12f);

        [Header("Throwing")]
        [SerializeField, Min(0f)] private float throwVelocity = 9f;
        [SerializeField, Min(0f)] private float throwUpwardVelocity = 0.75f;

        [Header("Highlight")]
        [SerializeField] private bool highlightTargets = true;
        [SerializeField, ColorUsage(true, true)] private Color highlightColor = new Color(1.2f, 1.05f, 0.35f, 1f);

        [Header("Held Collision")]
        [SerializeField, Min(0f)] private float collisionRestoreDelay = 0.2f;

        private readonly Dictionary<Renderer, MaterialPropertyBlock> originalBlocks =
            new Dictionary<Renderer, MaterialPropertyBlock>();

        private ApexGrabbable currentTarget;
        private Renderer[] highlightedRenderers = System.Array.Empty<Renderer>();
        private Coroutine restoreCollisionRoutine;

        public ApexGrabbable CurrentTarget => currentTarget;
        public float HoldDistance => holdDistance;

        private void Reset()
        {
            viewCamera = GetComponentInChildren<Camera>(true);
            grabber = GetComponentInChildren<ApexGrabber>(true);
            holdTarget = grabber != null ? grabber.GripTarget : null;
            playerColliders = GetComponentsInChildren<Collider>(true);
        }

        private void Awake()
        {
            ResolveReferences();
            ApplyHoldTargetPosition();
        }

        private void OnEnable()
        {
            ResolveReferences();
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

            ClearHighlight();
        }

        private void Update()
        {
            RefreshTarget();
        }

        public bool TryInteract()
        {
            if (grabber == null)
            {
                return false;
            }

            if (grabber.IsHolding)
            {
                grabber.Release();
                return true;
            }

            return currentTarget != null && grabber.TryGrab(currentTarget);
        }

        public void AdjustHoldDistance(float scrollDelta)
        {
            if (Mathf.Abs(scrollDelta) <= Mathf.Epsilon)
            {
                return;
            }

            holdDistance = Mathf.Clamp(
                holdDistance + scrollDelta * scrollSensitivity,
                minimumHoldDistance,
                maximumHoldDistance);
            ApplyHoldTargetPosition();
        }

        public bool ThrowHeld()
        {
            if (grabber == null || !grabber.IsHolding)
            {
                return false;
            }

            ApexGrabbable held = grabber.HeldObject;
            Rigidbody body = held != null && held.Body != null ? held.Body.Rigidbody : null;
            Vector3 direction = viewCamera != null ? viewCamera.transform.forward : transform.forward;
            Vector3 velocity = direction * throwVelocity + Vector3.up * throwUpwardVelocity;

            grabber.ForceRelease();
            if (body != null && !body.isKinematic)
            {
                body.velocity = velocity;
                body.WakeUp();
            }

            return true;
        }

        private void RefreshTarget()
        {
            if (grabber != null && grabber.IsHolding)
            {
                SetTarget(null);
                return;
            }

            if (viewCamera == null)
            {
                SetTarget(null);
                return;
            }

            Ray ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
            if (!Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    interactionDistance,
                    interactionLayers,
                    triggerInteraction))
            {
                SetTarget(null);
                return;
            }

            SetTarget(hit.collider != null
                ? hit.collider.GetComponentInParent<ApexGrabbable>()
                : null);
        }

        private void SetTarget(ApexGrabbable target)
        {
            if (currentTarget == target)
            {
                return;
            }

            ClearHighlight();
            currentTarget = target;

            if (!highlightTargets || currentTarget == null)
            {
                return;
            }

            highlightedRenderers = currentTarget.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < highlightedRenderers.Length; i++)
            {
                Renderer renderer = highlightedRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                MaterialPropertyBlock original = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(original);
                originalBlocks[renderer] = original;

                MaterialPropertyBlock highlighted = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(highlighted);
                highlighted.SetColor("_BaseColor", highlightColor);
                highlighted.SetColor("_Color", highlightColor);
                highlighted.SetColor("_EmissionColor", highlightColor * 0.35f);
                renderer.SetPropertyBlock(highlighted);
            }
        }

        private void ClearHighlight()
        {
            foreach (KeyValuePair<Renderer, MaterialPropertyBlock> pair in originalBlocks)
            {
                if (pair.Key != null)
                {
                    pair.Key.SetPropertyBlock(pair.Value);
                }
            }

            originalBlocks.Clear();
            highlightedRenderers = System.Array.Empty<Renderer>();
        }

        private void HandleGrabbed(ApexGrabbable held)
        {
            ClearHighlight();
            SetHeldCollisionIgnored(held, true);
        }

        private void HandleReleased(ApexGrabbable released)
        {
            if (restoreCollisionRoutine != null)
            {
                StopCoroutine(restoreCollisionRoutine);
            }

            restoreCollisionRoutine = StartCoroutine(RestoreCollisionAfterDelay(released));
        }

        private IEnumerator RestoreCollisionAfterDelay(ApexGrabbable released)
        {
            if (collisionRestoreDelay > 0f)
            {
                yield return new WaitForSeconds(collisionRestoreDelay);
            }

            SetHeldCollisionIgnored(released, false);
            restoreCollisionRoutine = null;
        }

        private void SetHeldCollisionIgnored(ApexGrabbable grabbable, bool ignored)
        {
            if (grabbable == null)
            {
                return;
            }

            Collider[] heldColliders = grabbable.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < playerColliders.Length; i++)
            {
                Collider playerCollider = playerColliders[i];
                if (playerCollider == null)
                {
                    continue;
                }

                for (int j = 0; j < heldColliders.Length; j++)
                {
                    Collider heldCollider = heldColliders[j];
                    if (heldCollider != null && heldCollider != playerCollider)
                    {
                        Physics.IgnoreCollision(playerCollider, heldCollider, ignored);
                    }
                }
            }
        }

        private void ResolveReferences()
        {
            if (viewCamera == null)
            {
                viewCamera = GetComponentInChildren<Camera>(true);
            }

            if (grabber == null)
            {
                grabber = GetComponentInChildren<ApexGrabber>(true);
            }

            if (holdTarget == null && grabber != null)
            {
                holdTarget = grabber.GripTarget;
            }

            if (playerColliders == null || playerColliders.Length == 0)
            {
                playerColliders = GetComponentsInChildren<Collider>(true);
            }
        }

        private void ApplyHoldTargetPosition()
        {
            if (holdTarget != null)
            {
                holdTarget.localPosition = new Vector3(holdOffset.x, holdOffset.y, holdDistance);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            interactionDistance = Mathf.Max(0.1f, interactionDistance);
            minimumHoldDistance = Mathf.Max(0.1f, minimumHoldDistance);
            maximumHoldDistance = Mathf.Max(minimumHoldDistance, maximumHoldDistance);
            holdDistance = Mathf.Clamp(holdDistance, minimumHoldDistance, maximumHoldDistance);
            scrollSensitivity = Mathf.Max(0.01f, scrollSensitivity);
            throwVelocity = Mathf.Max(0f, throwVelocity);
            throwUpwardVelocity = Mathf.Max(0f, throwUpwardVelocity);
            collisionRestoreDelay = Mathf.Max(0f, collisionRestoreDelay);
            ApplyHoldTargetPosition();
        }
#endif
    }
}
