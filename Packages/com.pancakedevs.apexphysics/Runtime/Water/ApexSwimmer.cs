using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Input-agnostic Rigidbody swimming motor. Feed movement through SetMoveInput,
    /// SetVerticalInput, and SetSprint from the Input System, AI, networking, or VR.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("Apex Physics Engine/Water/Swimmer")]
    public sealed class ApexSwimmer : MonoBehaviour, IApexWaterReactive
    {
        [Header("References")]
        [SerializeField] private Transform movementReference;
        [SerializeField] private Transform headReference;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float swimSpeed = 3f;
        [SerializeField, Min(1f)] private float sprintMultiplier = 1.8f;
        [SerializeField, Min(0f)] private float verticalSpeed = 2.5f;
        [SerializeField, Min(0f)] private float acceleration = 8f;
        [SerializeField, Min(0f)] private float turnSpeed = 180f;
        [SerializeField] private bool rotateTowardMovement = true;

        [Header("Surface Behaviour")]
        [SerializeField] private float surfaceOffset = 0.2f;
        [SerializeField, Min(0f)] private float surfaceBuoyancy = 15f;

        [Header("Oxygen")]
        [SerializeField] private bool useOxygen = true;
        [SerializeField, Min(0.1f)] private float maximumOxygen = 10f;
        [SerializeField, Min(0f)] private float oxygenDrainRate = 1f;
        [SerializeField, Min(0f)] private float oxygenRegenerationRate = 3f;
        [SerializeField] private UnityEvent outOfOxygen;

        private readonly List<ApexWaterVolume> activeVolumes = new List<ApexWaterVolume>();
        private Rigidbody body;
        private ApexWaterVolume activeVolume;
        private Vector2 moveInput;
        private float verticalInput;
        private bool sprintInput;
        private bool oxygenEventFired;

        public event Action OutOfOxygen;

        public bool IsSwimming => activeVolume != null;
        public bool IsHeadUnderwater { get; private set; }
        public float CurrentOxygen { get; private set; }
        public float Oxygen01 => maximumOxygen > 0f
            ? Mathf.Clamp01(CurrentOxygen / maximumOxygen)
            : 0f;
        public ApexWaterVolume ActiveVolume => activeVolume;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            CurrentOxygen = maximumOxygen;
        }

        private void OnDisable()
        {
            activeVolumes.Clear();
            activeVolume = null;
            IsHeadUnderwater = false;
        }

        public void OnApexWaterEnter(ApexWaterVolume volume)
        {
            if (volume != null && !activeVolumes.Contains(volume))
            {
                activeVolumes.Add(volume);
            }
            activeVolume = SelectActiveVolume();
            oxygenEventFired = false;
        }

        public void OnApexWaterExit(ApexWaterVolume volume)
        {
            activeVolumes.Remove(volume);
            activeVolume = SelectActiveVolume();
            if (activeVolume == null)
            {
                IsHeadUnderwater = false;
            }
        }

        public void SetMoveInput(Vector2 input)
        {
            moveInput = Vector2.ClampMagnitude(input, 1f);
        }

        public void SetVerticalInput(float input)
        {
            verticalInput = Mathf.Clamp(input, -1f, 1f);
        }

        public void SetSprint(bool sprint)
        {
            sprintInput = sprint;
        }

        public void SetInput(Vector2 movement, float vertical, bool sprint)
        {
            SetMoveInput(movement);
            SetVerticalInput(vertical);
            SetSprint(sprint);
        }

        public void ClearInput()
        {
            moveInput = Vector2.zero;
            verticalInput = 0f;
            sprintInput = false;
        }

        private void FixedUpdate()
        {
            activeVolume = SelectActiveVolume();
            if (activeVolume == null || body == null)
            {
                return;
            }

            Vector3 samplePosition = headReference != null
                ? headReference.position
                : transform.position + Vector3.up * surfaceOffset;
            float waterHeight = activeVolume.GetWaterHeight(samplePosition);
            IsHeadUnderwater = samplePosition.y < waterHeight;
            UpdateOxygen(Time.fixedDeltaTime);
            UpdateMovement(waterHeight);
        }

        private void UpdateMovement(float waterHeight)
        {
            Transform reference = movementReference != null ? movementReference : transform;
            Vector3 forward = Vector3.ProjectOnPlane(reference.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(reference.right, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = transform.forward;
            }
            if (right.sqrMagnitude < 0.0001f)
            {
                right = transform.right;
            }

            Vector3 planarDirection = forward * moveInput.y + right * moveInput.x;
            if (planarDirection.sqrMagnitude > 1f)
            {
                planarDirection.Normalize();
            }

            float speed = swimSpeed * (sprintInput ? sprintMultiplier : 1f);
            Vector3 targetVelocity = planarDirection * speed;

            if (Mathf.Abs(verticalInput) > 0.01f)
            {
                targetVelocity += Vector3.up * verticalInput * verticalSpeed;
            }
            else
            {
                float targetY = waterHeight - surfaceOffset;
                float difference = targetY - transform.position.y;
                targetVelocity += Vector3.up * Mathf.Clamp(
                    difference * surfaceBuoyancy * 0.1f,
                    -verticalSpeed,
                    verticalSpeed);
            }

            targetVelocity += activeVolume.GetCurrent(body.worldCenterOfMass);
            float floorHeight = activeVolume.GetFloorHeight(body.worldCenterOfMass);
            if (body.worldCenterOfMass.y + targetVelocity.y * Time.fixedDeltaTime < floorHeight)
            {
                targetVelocity.y = Mathf.Max(0f, targetVelocity.y);
            }

            body.linearVelocity = Vector3.MoveTowards(
                body.linearVelocity,
                targetVelocity,
                acceleration * Time.fixedDeltaTime);

            if (rotateTowardMovement && planarDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(planarDirection, Vector3.up);
                body.MoveRotation(Quaternion.RotateTowards(
                    body.rotation,
                    targetRotation,
                    turnSpeed * Time.fixedDeltaTime));
            }
        }

        private void UpdateOxygen(float deltaTime)
        {
            if (!useOxygen)
            {
                CurrentOxygen = maximumOxygen;
                return;
            }

            if (IsHeadUnderwater)
            {
                CurrentOxygen = Mathf.Max(
                    0f,
                    CurrentOxygen - oxygenDrainRate * deltaTime);
                if (CurrentOxygen <= 0f && !oxygenEventFired)
                {
                    oxygenEventFired = true;
                    outOfOxygen?.Invoke();
                    OutOfOxygen?.Invoke();
                }
            }
            else
            {
                CurrentOxygen = Mathf.Min(
                    maximumOxygen,
                    CurrentOxygen + oxygenRegenerationRate * deltaTime);
                oxygenEventFired = false;
            }
        }

        private ApexWaterVolume SelectActiveVolume()
        {
            ApexWaterVolume best = null;
            float highestSurface = float.NegativeInfinity;
            Vector3 sample = body != null ? body.worldCenterOfMass : transform.position;
            for (int i = activeVolumes.Count - 1; i >= 0; i--)
            {
                ApexWaterVolume volume = activeVolumes[i];
                if (volume == null || !volume.isActiveAndEnabled)
                {
                    activeVolumes.RemoveAt(i);
                    continue;
                }
                if (!volume.ContainsHorizontal(sample) || sample.y < volume.GetFloorHeight(sample))
                {
                    continue;
                }

                float surface = volume.GetWaterHeight(sample);
                if (surface > highestSurface)
                {
                    highestSurface = surface;
                    best = volume;
                }
            }
            return best;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            swimSpeed = Mathf.Max(0f, swimSpeed);
            sprintMultiplier = Mathf.Max(1f, sprintMultiplier);
            verticalSpeed = Mathf.Max(0f, verticalSpeed);
            acceleration = Mathf.Max(0f, acceleration);
            turnSpeed = Mathf.Max(0f, turnSpeed);
            surfaceBuoyancy = Mathf.Max(0f, surfaceBuoyancy);
            maximumOxygen = Mathf.Max(0.1f, maximumOxygen);
            oxygenDrainRate = Mathf.Max(0f, oxygenDrainRate);
            oxygenRegenerationRate = Mathf.Max(0f, oxygenRegenerationRate);
            if (!Application.isPlaying)
            {
                CurrentOxygen = maximumOxygen;
            }
        }
#endif
    }
}
