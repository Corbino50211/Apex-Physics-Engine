using System;
using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Read-only collision information produced by an <see cref="ApexBody"/>.
    /// Damage, audio, particles, haptics, and gameplay systems can consume this
    /// without being tightly coupled to the body component.
    /// </summary>
    [Serializable]
    public struct ApexImpactInfo
    {
        public ApexImpactInfo(
            ApexBody source,
            GameObject otherObject,
            Rigidbody otherRigidbody,
            Collider otherCollider,
            Vector3 point,
            Vector3 normal,
            Vector3 relativeVelocity,
            float impulse)
        {
            Source = source;
            OtherObject = otherObject;
            OtherRigidbody = otherRigidbody;
            OtherCollider = otherCollider;
            Point = point;
            Normal = normal;
            RelativeVelocity = relativeVelocity;
            Impulse = impulse;
        }

        public ApexBody Source { get; }
        public GameObject OtherObject { get; }
        public Rigidbody OtherRigidbody { get; }
        public Collider OtherCollider { get; }
        public Vector3 Point { get; }
        public Vector3 Normal { get; }
        public Vector3 RelativeVelocity { get; }
        public float Impulse { get; }
        public float Speed => RelativeVelocity.magnitude;
    }
}
