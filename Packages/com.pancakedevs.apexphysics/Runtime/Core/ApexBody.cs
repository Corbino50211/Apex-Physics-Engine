using System;
using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Core Apex component for a simulated object. It wraps a Rigidbody with
    /// reusable profiles, safe force helpers, velocity limits, teleportation,
    /// and universal impact reporting.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ApexBody : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private ApexPhysicsProfile profile;
        [SerializeField] private bool applyProfileOnAwake = true;

        [Header("Safety")]
        [SerializeField] private bool limitVelocity = true;
        [SerializeField, Min(0.01f)] private float fallbackMaximumLinearSpeed = 75f;
        [SerializeField, Min(0.01f)] private float fallbackMaximumAngularSpeed = 50f;

        private Rigidbody cachedRigidbody;

        public event Action<ApexImpactInfo> Impacted;

        public Rigidbody Rigidbody
        {
            get
            {
                CacheRigidbody();
                return cachedRigidbody;
            }
        }

        public ApexPhysicsProfile Profile => profile;
        public float Mass => Rigidbody != null ? Rigidbody.mass : 0f;
        public Vector3 WorldCenterOfMass => Rigidbody != null ? Rigidbody.worldCenterOfMass : transform.position;
        public bool IsSleeping => Rigidbody != null && Rigidbody.IsSleeping();

        private void Reset()
        {
            CacheRigidbody();
        }

        private void Awake()
        {
            CacheRigidbody();

            if (applyProfileOnAwake)
            {
                ApplyProfile();
            }
        }

        private void FixedUpdate()
        {
            if (!limitVelocity || Rigidbody == null || Rigidbody.isKinematic)
            {
                return;
            }

            float maximumLinearSpeed = profile != null
                ? profile.MaximumLinearSpeed
                : fallbackMaximumLinearSpeed;

            float maximumAngularSpeed = profile != null
                ? profile.MaximumAngularSpeed
                : fallbackMaximumAngularSpeed;

            Rigidbody.velocity = Vector3.ClampMagnitude(Rigidbody.velocity, maximumLinearSpeed);
            Rigidbody.angularVelocity = Vector3.ClampMagnitude(Rigidbody.angularVelocity, maximumAngularSpeed);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision == null)
            {
                return;
            }

            Vector3 point = transform.position;
            Vector3 normal = Vector3.up;
            Collider otherCollider = collision.collider;

            if (collision.contactCount > 0)
            {
                ContactPoint contact = collision.GetContact(0);
                point = contact.point;
                normal = contact.normal;
                otherCollider = contact.otherCollider;
            }

            ApexImpactInfo impact = new ApexImpactInfo(
                this,
                collision.gameObject,
                collision.rigidbody,
                otherCollider,
                point,
                normal,
                collision.relativeVelocity,
                collision.impulse.magnitude);

            Impacted?.Invoke(impact);
        }

        /// <summary>Assigns and optionally applies a reusable physics profile.</summary>
        public void SetProfile(ApexPhysicsProfile newProfile, bool applyImmediately = true)
        {
            profile = newProfile;

            if (applyImmediately)
            {
                ApplyProfile();
            }
        }

        /// <summary>Applies the currently assigned profile to the Rigidbody.</summary>
        public void ApplyProfile()
        {
            CacheRigidbody();
            profile?.ApplyTo(cachedRigidbody);
        }

        public void ApplyForce(Vector3 force, ForceMode mode = ForceMode.Force)
        {
            if (CanSimulate())
            {
                Rigidbody.AddForce(force, mode);
            }
        }

        public void ApplyForceAtPosition(Vector3 force, Vector3 worldPosition, ForceMode mode = ForceMode.Force)
        {
            if (CanSimulate())
            {
                Rigidbody.AddForceAtPosition(force, worldPosition, mode);
            }
        }

        public void ApplyImpulse(Vector3 impulse)
        {
            ApplyForce(impulse, ForceMode.Impulse);
        }

        public void ApplyTorque(Vector3 torque, ForceMode mode = ForceMode.Force)
        {
            if (CanSimulate())
            {
                Rigidbody.AddTorque(torque, mode);
            }
        }

        public void ApplyTorqueImpulse(Vector3 impulse)
        {
            ApplyTorque(impulse, ForceMode.Impulse);
        }

        public void Wake()
        {
            Rigidbody?.WakeUp();
        }

        public void Sleep()
        {
            Rigidbody?.Sleep();
        }

        public void StopMotion()
        {
            if (Rigidbody == null)
            {
                return;
            }

            Rigidbody.velocity = Vector3.zero;
            Rigidbody.angularVelocity = Vector3.zero;
        }

        /// <summary>
        /// Repositions the body without leaving stale velocity unless requested.
        /// Intended for resets, checkpoints, respawns, and recovery systems.
        /// </summary>
        public void Teleport(Vector3 position, Quaternion rotation, bool preserveVelocity = false)
        {
            if (Rigidbody == null)
            {
                transform.SetPositionAndRotation(position, rotation);
                return;
            }

            Vector3 previousVelocity = Rigidbody.velocity;
            Vector3 previousAngularVelocity = Rigidbody.angularVelocity;

            Rigidbody.position = position;
            Rigidbody.rotation = rotation;

            if (preserveVelocity)
            {
                Rigidbody.velocity = previousVelocity;
                Rigidbody.angularVelocity = previousAngularVelocity;
            }
            else
            {
                StopMotion();
            }

            Physics.SyncTransforms();
            Rigidbody.WakeUp();
        }

        private bool CanSimulate()
        {
            return Rigidbody != null && Rigidbody.gameObject.activeInHierarchy && !Rigidbody.isKinematic;
        }

        private void CacheRigidbody()
        {
            if (cachedRigidbody == null)
            {
                cachedRigidbody = GetComponent<Rigidbody>();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            fallbackMaximumLinearSpeed = Mathf.Max(0.01f, fallbackMaximumLinearSpeed);
            fallbackMaximumAngularSpeed = Mathf.Max(0.01f, fallbackMaximumAngularSpeed);
            CacheRigidbody();
        }
#endif
    }
}
