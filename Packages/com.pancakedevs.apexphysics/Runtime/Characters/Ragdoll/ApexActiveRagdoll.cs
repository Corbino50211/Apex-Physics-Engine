using System;
using System.Collections.Generic;
using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Drives a physical ragdoll toward a separate animated target skeleton.
    /// Each physical bone requires an ApexRagdollBone with a matching target.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ApexActiveRagdoll : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private ApexRagdollProfile profile;
        [SerializeField] private Animator targetAnimator;
        [SerializeField] private ApexRagdollBone rootBone;
        [SerializeField] private List<ApexRagdollBone> bones = new List<ApexRagdollBone>();
        [SerializeField] private bool automaticallyFindBones = true;

        [Header("State")]
        [SerializeField] private ApexRagdollState startingState = ApexRagdollState.Active;
        [SerializeField, Range(0f, 1f)] private float globalStrength = 1f;

        [Header("Root Following")]
        [SerializeField] private bool driveRootPosition = true;
        [SerializeField] private bool driveRootRotation = true;

        private ApexRagdollState currentState;
        private float stateStartTime;
        private bool subscribed;

        public event Action<ApexRagdollState> StateChanged;
        public event Action<ApexRagdollBone, Collision> KnockedDown;
        public event Action Recovered;

        public ApexRagdollProfile Profile => profile;
        public Animator TargetAnimator => targetAnimator;
        public ApexRagdollBone RootBone => rootBone;
        public IReadOnlyList<ApexRagdollBone> Bones => bones;
        public ApexRagdollState State => currentState;
        public float GlobalStrength => globalStrength;
        public float RecoveryProgress
        {
            get
            {
                if (currentState != ApexRagdollState.Recovering || profile == null)
                {
                    return currentState == ApexRagdollState.Active ? 1f : 0f;
                }

                return Mathf.Clamp01((Time.time - stateStartTime) / profile.RecoveryBlendDuration);
            }
        }

        private void Reset()
        {
            RefreshBones();
        }

        private void Awake()
        {
            if (automaticallyFindBones || bones.Count == 0)
            {
                RefreshBones();
            }

            CaptureCurrentPose();
            currentState = startingState;
            stateStartTime = Time.time;
        }

        private void OnEnable()
        {
            SubscribeToBones();
        }

        private void OnDisable()
        {
            UnsubscribeFromBones();
        }

        private void FixedUpdate()
        {
            UpdateStateMachine();

            float strength = GetStateStrength() * globalStrength;
            for (int i = 0; i < bones.Count; i++)
            {
                ApexRagdollBone bone = bones[i];
                if (bone == null)
                {
                    continue;
                }

                if (strength > 0f)
                {
                    bone.ApplyMuscle(profile, strength);
                }
                else
                {
                    bone.DisableMuscle();
                }
            }

            if (strength > 0f)
            {
                DriveRoot(strength);
            }
        }

        public void SetProfile(ApexRagdollProfile newProfile)
        {
            profile = newProfile;
        }

        public void SetGlobalStrength(float strength)
        {
            globalStrength = Mathf.Clamp01(strength);
        }

        public void SetState(ApexRagdollState newState)
        {
            if (currentState == newState)
            {
                return;
            }

            currentState = newState;
            stateStartTime = Time.time;
            StateChanged?.Invoke(currentState);

            if (currentState == ApexRagdollState.Active)
            {
                Recovered?.Invoke();
            }
        }

        public void KnockDown()
        {
            SetState(ApexRagdollState.Limp);
        }

        public void BeginRecovery()
        {
            if (currentState != ApexRagdollState.Active)
            {
                SetState(ApexRagdollState.Recovering);
            }
        }

        public void RecoverImmediately()
        {
            SetState(ApexRagdollState.Active);
        }

        public void RefreshBones()
        {
            bool wasSubscribed = subscribed;
            if (wasSubscribed)
            {
                UnsubscribeFromBones();
            }

            bones.Clear();
            ApexRagdollBone[] foundBones = GetComponentsInChildren<ApexRagdollBone>(true);
            for (int i = 0; i < foundBones.Length; i++)
            {
                ApexRagdollBone bone = foundBones[i];
                if (bone != null)
                {
                    bones.Add(bone);
                    if (rootBone == null && bone.IsRootBone)
                    {
                        rootBone = bone;
                    }
                }
            }

            if (rootBone == null && bones.Count > 0)
            {
                rootBone = bones[0];
            }

            if (wasSubscribed || isActiveAndEnabled)
            {
                SubscribeToBones();
            }
        }

        public void CaptureCurrentPose()
        {
            for (int i = 0; i < bones.Count; i++)
            {
                bones[i]?.CapturePose();
            }
        }

        private void UpdateStateMachine()
        {
            if (profile == null)
            {
                return;
            }

            float elapsed = Time.time - stateStartTime;
            if (currentState == ApexRagdollState.Limp &&
                profile.AutomaticallyRecover &&
                elapsed >= profile.LimpDuration)
            {
                SetState(ApexRagdollState.Recovering);
                return;
            }

            if (currentState == ApexRagdollState.Recovering &&
                elapsed >= profile.RecoveryBlendDuration)
            {
                SetState(ApexRagdollState.Active);
            }
        }

        private float GetStateStrength()
        {
            if (profile == null)
            {
                return 0f;
            }

            switch (currentState)
            {
                case ApexRagdollState.Active:
                    return 1f;
                case ApexRagdollState.Recovering:
                    return RecoveryProgress;
                default:
                    return 0f;
            }
        }

        private void DriveRoot(float strength)
        {
            if (profile == null || rootBone == null || rootBone.Target == null || rootBone.Body == null)
            {
                return;
            }

            Rigidbody body = rootBone.Body;
            Transform target = rootBone.Target;

            if (driveRootPosition)
            {
                Vector3 positionError = target.position - body.position;
                Vector3 force = positionError * profile.RootPositionSpring -
                                body.velocity * profile.RootPositionDamper;
                force = Vector3.ClampMagnitude(force, profile.MaximumRootForce) * strength;
                body.AddForce(force, ForceMode.Force);
            }

            if (driveRootRotation)
            {
                Quaternion delta = target.rotation * Quaternion.Inverse(body.rotation);
                delta.ToAngleAxis(out float angleDegrees, out Vector3 axis);
                if (angleDegrees > 180f)
                {
                    angleDegrees -= 360f;
                }

                if (axis.sqrMagnitude > 0.0001f && !float.IsNaN(axis.x))
                {
                    Vector3 torque = axis.normalized *
                                     (angleDegrees * Mathf.Deg2Rad * profile.RootRotationSpring) -
                                     body.angularVelocity * profile.RootRotationDamper;
                    torque = Vector3.ClampMagnitude(torque, profile.MaximumRootTorque) * strength;
                    body.AddTorque(torque, ForceMode.Force);
                }
            }
        }

        private void SubscribeToBones()
        {
            if (subscribed)
            {
                return;
            }

            for (int i = 0; i < bones.Count; i++)
            {
                if (bones[i] != null)
                {
                    bones[i].Impacted += HandleBoneImpact;
                }
            }

            subscribed = true;
        }

        private void UnsubscribeFromBones()
        {
            if (!subscribed)
            {
                return;
            }

            for (int i = 0; i < bones.Count; i++)
            {
                if (bones[i] != null)
                {
                    bones[i].Impacted -= HandleBoneImpact;
                }
            }

            subscribed = false;
        }

        private void HandleBoneImpact(ApexRagdollBone bone, Collision collision)
        {
            if (profile == null || collision == null || currentState == ApexRagdollState.Limp)
            {
                return;
            }

            bool hardEnough = collision.relativeVelocity.magnitude >= profile.KnockdownImpactSpeed ||
                              collision.impulse.magnitude >= profile.KnockdownImpactImpulse;
            if (!hardEnough)
            {
                return;
            }

            KnockedDown?.Invoke(bone, collision);
            KnockDown();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            globalStrength = Mathf.Clamp01(globalStrength);
            if (automaticallyFindBones && !Application.isPlaying)
            {
                RefreshBones();
            }
        }
#endif
    }
}
