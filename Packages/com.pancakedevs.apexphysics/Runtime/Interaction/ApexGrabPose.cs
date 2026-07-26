using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// The local point and orientation a grabber drives while holding an object.
    /// It is generated from either an ApexGrabPoint or a free-grab position.
    /// </summary>
    public readonly struct ApexGrabPose
    {
        public ApexGrabPose(
            ApexGrabPoint sourcePoint,
            Vector3 localPosition,
            Quaternion localRotation,
            bool followRotation)
        {
            SourcePoint = sourcePoint;
            LocalPosition = localPosition;
            LocalRotation = localRotation;
            FollowRotation = followRotation;
        }

        public ApexGrabPoint SourcePoint { get; }
        public Vector3 LocalPosition { get; }
        public Quaternion LocalRotation { get; }
        public bool FollowRotation { get; }
    }
}
