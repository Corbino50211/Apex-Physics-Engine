using System.Collections.Generic;
using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Preserves the authoritative PC motor's movement when an animated character is
    /// released into ragdoll. Each bone receives the point velocity it had around the
    /// moving and turning motor, preventing button-triggered knockdowns from freezing
    /// in place before falling.
    /// </summary>
    [DefaultExecutionOrder(-255)]
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class ApexPCRagdollMomentum : MonoBehaviour
    {
        [SerializeField] private ApexPCPhysicalCharacter character;
        [SerializeField, Range(0f, 2f)] private float linearMomentumMultiplier = 1f;
        [SerializeField, Range(0f, 2f)] private float angularMomentumMultiplier = 1f;
        [SerializeField, Min(0f)] private float maximumTransferredSpeed = 20f;

        private readonly List<Rigidbody> boneBodies = new List<Rigidbody>();

        private Rigidbody motorBody;
        private Vector3 cachedLinearVelocity;
        private Vector3 cachedAngularVelocity;
        private Vector3 cachedCenterOfMass;
        private bool subscribed;

        private void Awake()
        {
            Configure(character != null ? character : GetComponent<ApexPCPhysicalCharacter>());
        }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void FixedUpdate()
        {
            if (!ResolveReferences())
            {
                return;
            }

            if (character.State != ApexPCCharacterState.Active &&
                character.State != ApexPCCharacterState.Staggered)
            {
                return;
            }

            cachedLinearVelocity = motorBody.velocity;
            cachedAngularVelocity = motorBody.angularVelocity;
            cachedCenterOfMass = motorBody.worldCenterOfMass;
        }

        public void Configure(ApexPCPhysicalCharacter owner)
        {
            Unsubscribe();
            character = owner;
            motorBody = owner != null ? owner.MotorBody : null;
            CacheBones();
            CaptureNow();
            Subscribe();
        }

        public void CaptureNow()
        {
            if (!ResolveReferences())
            {
                return;
            }

            cachedLinearVelocity = motorBody.velocity;
            cachedAngularVelocity = motorBody.angularVelocity;
            cachedCenterOfMass = motorBody.worldCenterOfMass;
        }

        private bool ResolveReferences()
        {
            if (character == null)
            {
                character = GetComponent<ApexPCPhysicalCharacter>();
            }

            if (character == null)
            {
                return false;
            }

            if (motorBody == null)
            {
                motorBody = character.MotorBody;
            }

            if (boneBodies.Count == 0)
            {
                CacheBones();
            }

            return motorBody != null && boneBodies.Count > 0;
        }

        private void CacheBones()
        {
            boneBodies.Clear();
            if (character == null || character.Humanoid == null ||
                character.Humanoid.PhysicalCharacter == null)
            {
                return;
            }

            ApexRagdollBone[] bones =
                character.Humanoid.PhysicalCharacter.GetComponentsInChildren<ApexRagdollBone>(true);
            for (int i = 0; i < bones.Length; i++)
            {
                Rigidbody body = bones[i] != null ? bones[i].Body : null;
                if (body != null && !boneBodies.Contains(body))
                {
                    boneBodies.Add(body);
                }
            }
        }

        private void HandleStateChanged(ApexPCCharacterState state)
        {
            if (state == ApexPCCharacterState.Ragdoll)
            {
                TransferMomentum();
                return;
            }

            if (state == ApexPCCharacterState.Active)
            {
                CaptureNow();
            }
        }

        private void TransferMomentum()
        {
            float maximumSpeedSquared = maximumTransferredSpeed > 0f
                ? maximumTransferredSpeed * maximumTransferredSpeed
                : float.PositiveInfinity;

            for (int i = 0; i < boneBodies.Count; i++)
            {
                Rigidbody body = boneBodies[i];
                if (body == null || body.isKinematic)
                {
                    continue;
                }

                Vector3 radius = body.worldCenterOfMass - cachedCenterOfMass;
                Vector3 pointVelocity =
                    cachedLinearVelocity * linearMomentumMultiplier +
                    Vector3.Cross(cachedAngularVelocity, radius) * angularMomentumMultiplier;

                if (pointVelocity.sqrMagnitude > maximumSpeedSquared)
                {
                    pointVelocity = pointVelocity.normalized * maximumTransferredSpeed;
                }

                body.velocity = pointVelocity;
                body.angularVelocity = cachedAngularVelocity * angularMomentumMultiplier;
                body.WakeUp();
            }
        }

        private void Subscribe()
        {
            if (subscribed || character == null)
            {
                return;
            }

            character.StateChanged += HandleStateChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || character == null)
            {
                subscribed = false;
                return;
            }

            character.StateChanged -= HandleStateChanged;
            subscribed = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            linearMomentumMultiplier = Mathf.Clamp(linearMomentumMultiplier, 0f, 2f);
            angularMomentumMultiplier = Mathf.Clamp(angularMomentumMultiplier, 0f, 2f);
            maximumTransferredSpeed = Mathf.Max(0f, maximumTransferredSpeed);
        }
#endif
    }
}
