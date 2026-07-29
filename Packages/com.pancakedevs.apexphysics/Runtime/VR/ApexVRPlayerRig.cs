using UnityEngine;
using UnityEngine.XR;

namespace PancakeDevs.ApexPhysics
{
    public enum ApexVRBackend
    {
        OpenXR,
        SteamVROpenVR
    }

    /// <summary>
    /// Marker and shared references for an Apex VR player. Both supported backends
    /// use Unity's XR device layer so the runtime package has no hard Valve dependency.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ApexVRPlayerRig : MonoBehaviour
    {
        [SerializeField] private ApexVRBackend backend = ApexVRBackend.OpenXR;
        [SerializeField] private Transform head;
        [SerializeField] private ApexVRPhysicalHand leftHand;
        [SerializeField] private ApexVRPhysicalHand rightHand;

        public ApexVRBackend Backend => backend;
        public Transform Head => head;
        public ApexVRPhysicalHand LeftHand => leftHand;
        public ApexVRPhysicalHand RightHand => rightHand;

#if UNITY_EDITOR
        public void EditorConfigure(
            ApexVRBackend selectedBackend,
            Transform trackedHead,
            ApexVRPhysicalHand trackedLeftHand,
            ApexVRPhysicalHand trackedRightHand)
        {
            backend = selectedBackend;
            head = trackedHead;
            leftHand = trackedLeftHand;
            rightHand = trackedRightHand;
        }
#endif
    }

    /// <summary>Copies one Unity XR node pose into a local tracking target.</summary>
    [DisallowMultipleComponent]
    public sealed class ApexVRTrackedNode : MonoBehaviour
    {
        [SerializeField] private XRNode node = XRNode.Head;
        [SerializeField] private bool trackPosition = true;
        [SerializeField] private bool trackRotation = true;

        public XRNode Node => node;
        public bool IsTracked { get; private set; }

        private void Update()
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            IsTracked = device.isValid;
            if (!IsTracked)
            {
                return;
            }

            if (trackPosition &&
                device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position))
            {
                transform.localPosition = position;
            }

            if (trackRotation &&
                device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation))
            {
                transform.localRotation = rotation;
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(XRNode trackedNode)
        {
            node = trackedNode;
        }
#endif
    }

    /// <summary>
    /// Kinematic physical proxy that follows a tracked controller and carries an
    /// ApexGrabber. It can push dynamic props while remaining stable under tracking.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ApexVRPhysicalHand : MonoBehaviour
    {
        [SerializeField] private Transform trackingTarget;
        [SerializeField] private ApexGrabber grabber;
        [SerializeField] private Collider handCollider;
        [SerializeField] private Collider[] playerColliders = System.Array.Empty<Collider>();
        [SerializeField, Min(0f)] private float maximumFollowDistance = 0.75f;

        private Rigidbody handBody;

        public Transform TrackingTarget => trackingTarget;
        public ApexGrabber Grabber => grabber;

        private void Awake()
        {
            handBody = GetComponent<Rigidbody>();
            if (grabber == null)
            {
                grabber = GetComponent<ApexGrabber>();
            }
            if (handCollider == null)
            {
                handCollider = GetComponent<Collider>();
            }

            IgnorePlayerCollisions();
        }

        private void FixedUpdate()
        {
            if (trackingTarget == null || handBody == null)
            {
                return;
            }

            Vector3 targetPosition = trackingTarget.position;
            Vector3 offset = targetPosition - handBody.position;
            if (maximumFollowDistance > 0f && offset.magnitude > maximumFollowDistance)
            {
                handBody.position = targetPosition;
                handBody.rotation = trackingTarget.rotation;
                return;
            }

            handBody.MovePosition(targetPosition);
            handBody.MoveRotation(trackingTarget.rotation);
        }

        private void IgnorePlayerCollisions()
        {
            if (handCollider == null || playerColliders == null)
            {
                return;
            }

            for (int i = 0; i < playerColliders.Length; i++)
            {
                Collider playerCollider = playerColliders[i];
                if (playerCollider != null && playerCollider != handCollider)
                {
                    Physics.IgnoreCollision(handCollider, playerCollider, true);
                }
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            Transform target,
            ApexGrabber handGrabber,
            Collider physicalCollider,
            Collider[] bodyColliders)
        {
            trackingTarget = target;
            grabber = handGrabber;
            handCollider = physicalCollider;
            playerColliders = bodyColliders ?? System.Array.Empty<Collider>();
        }
#endif
    }

    /// <summary>Rigidbody smooth locomotion and snap turning for the Apex VR rig.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public sealed class ApexVRPlayerMotor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform head;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float moveSpeed = 3f;
        [SerializeField, Min(0f)] private float acceleration = 18f;
        [SerializeField, Min(0f)] private float jumpVelocity = 4.5f;
        [SerializeField] private LayerMask groundLayers = ~0;
        [SerializeField, Min(0.01f)] private float groundProbeDistance = 0.12f;

        [Header("Body")]
        [SerializeField, Min(0.5f)] private float minimumHeight = 0.8f;
        [SerializeField, Min(0.5f)] private float maximumHeight = 2.2f;

        private Rigidbody body;
        private CapsuleCollider capsule;
        private Vector2 moveInput;
        private bool jumpQueued;
        private float queuedSnapTurn;

        public bool IsGrounded { get; private set; }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            capsule = GetComponent<CapsuleCollider>();
        }

        private void FixedUpdate()
        {
            UpdateCapsuleFromHead();
            UpdateGrounded();
            ApplyMovement();
            ApplySnapTurn();

            if (jumpQueued && IsGrounded)
            {
                Vector3 velocity = body.velocity;
                velocity.y = jumpVelocity;
                body.velocity = velocity;
            }

            jumpQueued = false;
        }

        public void SetMoveInput(Vector2 input)
        {
            moveInput = Vector2.ClampMagnitude(input, 1f);
        }

        public void QueueJump()
        {
            jumpQueued = true;
        }

        public void QueueSnapTurn(float degrees)
        {
            queuedSnapTurn += degrees;
        }

        public void ClearInput()
        {
            moveInput = Vector2.zero;
            jumpQueued = false;
            queuedSnapTurn = 0f;
        }

        private void ApplyMovement()
        {
            if (head == null)
            {
                return;
            }

            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(head.right, Vector3.up).normalized;
            Vector3 desired = (forward * moveInput.y + right * moveInput.x) * moveSpeed;
            Vector3 current = Vector3.ProjectOnPlane(body.velocity, Vector3.up);
            Vector3 change = Vector3.ClampMagnitude(desired - current, acceleration * Time.fixedDeltaTime);
            body.AddForce(change, ForceMode.VelocityChange);
        }

        private void ApplySnapTurn()
        {
            if (head == null || Mathf.Abs(queuedSnapTurn) < 0.01f)
            {
                queuedSnapTurn = 0f;
                return;
            }

            Quaternion delta = Quaternion.Euler(0f, queuedSnapTurn, 0f);
            Vector3 pivot = head.position;
            Vector3 relativeRoot = body.position - pivot;
            body.MoveRotation(delta * body.rotation);
            body.MovePosition(pivot + delta * relativeRoot);
            queuedSnapTurn = 0f;
        }

        private void UpdateCapsuleFromHead()
        {
            if (head == null || capsule == null)
            {
                return;
            }

            Vector3 localHead = transform.InverseTransformPoint(head.position);
            float height = Mathf.Clamp(localHead.y, minimumHeight, maximumHeight);
            capsule.height = Mathf.Max(height, capsule.radius * 2f);
            capsule.center = new Vector3(localHead.x, capsule.height * 0.5f, localHead.z);
        }

        private void UpdateGrounded()
        {
            Vector3 center = transform.TransformPoint(capsule.center);
            float bottomOffset = Mathf.Max(0f, capsule.height * 0.5f - capsule.radius);
            Vector3 origin = center - transform.up * bottomOffset;
            IsGrounded = Physics.SphereCast(
                origin,
                capsule.radius * 0.9f,
                Vector3.down,
                out _,
                groundProbeDistance,
                groundLayers,
                QueryTriggerInteraction.Ignore);
        }

#if UNITY_EDITOR
        public void EditorConfigure(Transform trackedHead)
        {
            head = trackedHead;
        }
#endif
    }

    /// <summary>
    /// Backend-neutral Unity XR input adapter. Left stick moves, right stick snap-turns,
    /// controller grip grabs, and the left primary button jumps.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ApexVRInput : MonoBehaviour
    {
        [SerializeField] private ApexVRPlayerMotor motor;
        [SerializeField] private ApexGrabber leftGrabber;
        [SerializeField] private ApexGrabber rightGrabber;
        [SerializeField, Range(10f, 90f)] private float snapTurnDegrees = 45f;
        [SerializeField, Range(0.1f, 1f)] private float turnThreshold = 0.7f;

        private bool leftGripHeld;
        private bool rightGripHeld;
        private bool turnLatched;
        private bool jumpHeld;

        private void Update()
        {
            InputDevice left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            InputDevice right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

            Vector2 move = Vector2.zero;
            if (left.isValid)
            {
                left.TryGetFeatureValue(CommonUsages.primary2DAxis, out move);
            }
            motor?.SetMoveInput(move);

            Vector2 turn = Vector2.zero;
            if (right.isValid)
            {
                right.TryGetFeatureValue(CommonUsages.primary2DAxis, out turn);
            }

            if (!turnLatched && Mathf.Abs(turn.x) >= turnThreshold)
            {
                motor?.QueueSnapTurn(Mathf.Sign(turn.x) * snapTurnDegrees);
                turnLatched = true;
            }
            else if (Mathf.Abs(turn.x) < turnThreshold * 0.5f)
            {
                turnLatched = false;
            }

            UpdateGrab(left, leftGrabber, ref leftGripHeld);
            UpdateGrab(right, rightGrabber, ref rightGripHeld);

            bool jumpPressed = false;
            if (left.isValid)
            {
                left.TryGetFeatureValue(CommonUsages.primaryButton, out jumpPressed);
            }
            if (jumpPressed && !jumpHeld)
            {
                motor?.QueueJump();
            }
            jumpHeld = jumpPressed;
        }

        private static void UpdateGrab(
            InputDevice device,
            ApexGrabber grabber,
            ref bool previousHeld)
        {
            bool held = false;
            if (device.isValid)
            {
                device.TryGetFeatureValue(CommonUsages.gripButton, out held);
            }

            if (held && !previousHeld)
            {
                grabber?.TryGrabClosest();
            }
            else if (!held && previousHeld)
            {
                grabber?.Release();
            }

            previousHeld = held;
        }

        private void OnDisable()
        {
            motor?.ClearInput();
            leftGrabber?.ForceRelease();
            rightGrabber?.ForceRelease();
            leftGripHeld = false;
            rightGripHeld = false;
            turnLatched = false;
            jumpHeld = false;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            ApexVRPlayerMotor playerMotor,
            ApexGrabber leftHandGrabber,
            ApexGrabber rightHandGrabber)
        {
            motor = playerMotor;
            leftGrabber = leftHandGrabber;
            rightGrabber = rightHandGrabber;
        }
#endif
    }
}
