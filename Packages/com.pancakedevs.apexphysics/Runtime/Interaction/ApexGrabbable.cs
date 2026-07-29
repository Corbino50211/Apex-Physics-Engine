using System;
using System.Collections.Generic;
using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Marks an ApexBody as holdable. It manages authored grab points, free grabbing,
    /// multiple grabbers, stealing rules, and the minimum number of hands required
    /// before the object can be physically moved.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ApexBody))]
    public sealed class ApexGrabbable : MonoBehaviour
    {
        [Header("Grab Rules")]
        [SerializeField] private bool allowFreeGrab = true;
        [SerializeField] private bool allowStealing;
        [SerializeField, Min(1)] private int maximumGrabbers = 2;
        [SerializeField, Min(1)] private int minimumHandsToMove = 1;

        [Header("Discovery")]
        [SerializeField] private bool includeInactiveGrabPoints = true;

        private readonly List<IApexGrabber> activeGrabbers = new List<IApexGrabber>(2);
        private ApexGrabPoint[] grabPoints = Array.Empty<ApexGrabPoint>();
        private Collider[] cachedColliders = Array.Empty<Collider>();
        private ApexBody cachedBody;

        public event Action<IApexGrabber, ApexGrabPose> Grabbed;
        public event Action<IApexGrabber> Released;

        public ApexBody Body
        {
            get
            {
                CacheReferences();
                return cachedBody;
            }
        }

        public bool AllowFreeGrab => allowFreeGrab;
        public bool AllowStealing => allowStealing;
        public int MaximumGrabbers => maximumGrabbers;
        public int MinimumHandsToMove => minimumHandsToMove;
        public int ActiveGrabberCount => activeGrabbers.Count;
        public bool IsGrabbed => activeGrabbers.Count > 0;
        public bool CanMove => activeGrabbers.Count >= minimumHandsToMove;
        public IReadOnlyList<IApexGrabber> ActiveGrabbers => activeGrabbers;

        private void Reset()
        {
            CacheReferences();
            RefreshGrabData();
        }

        private void Awake()
        {
            CacheReferences();
            RefreshGrabData();
        }

        private void OnDisable()
        {
            for (int i = activeGrabbers.Count - 1; i >= 0; i--)
            {
                activeGrabbers[i]?.ForceRelease();
            }

            activeGrabbers.Clear();
        }

        public void RefreshGrabData()
        {
            grabPoints = GetComponentsInChildren<ApexGrabPoint>(includeInactiveGrabPoints);
            cachedColliders = GetComponentsInChildren<Collider>(true);
        }

        public bool TryGetBestGrabPose(
            Vector3 grabberPosition,
            Quaternion grabberRotation,
            ApexHandedness handedness,
            out ApexGrabPose pose)
        {
            CacheReferences();

            ApexGrabPoint bestPoint = null;
            float bestDistance = float.PositiveInfinity;
            int bestPriority = int.MinValue;

            for (int i = 0; i < grabPoints.Length; i++)
            {
                ApexGrabPoint point = grabPoints[i];
                if (point == null || !point.isActiveAndEnabled)
                {
                    continue;
                }

                float distance = Vector3.Distance(grabberPosition, point.transform.position);
                if (!point.CanBeUsedBy(handedness, distance))
                {
                    continue;
                }

                if (point.Priority > bestPriority ||
                    (point.Priority == bestPriority && distance < bestDistance))
                {
                    bestPoint = point;
                    bestPriority = point.Priority;
                    bestDistance = distance;
                }
            }

            if (bestPoint != null)
            {
                pose = new ApexGrabPose(
                    bestPoint,
                    transform.InverseTransformPoint(bestPoint.transform.position),
                    Quaternion.Inverse(transform.rotation) * bestPoint.transform.rotation,
                    bestPoint.FollowRotation);
                return true;
            }

            if (!allowFreeGrab)
            {
                pose = default;
                return false;
            }

            Vector3 closestPoint = FindClosestPoint(grabberPosition);
            pose = new ApexGrabPose(
                null,
                transform.InverseTransformPoint(closestPoint),
                Quaternion.Inverse(transform.rotation) * grabberRotation,
                true);
            return true;
        }

        public bool TryBeginGrab(IApexGrabber grabber, ApexGrabPose pose)
        {
            if (grabber == null)
            {
                return false;
            }

            if (activeGrabbers.Contains(grabber))
            {
                return true;
            }

            if (activeGrabbers.Count >= maximumGrabbers)
            {
                if (!allowStealing)
                {
                    return false;
                }

                IApexGrabber oldestGrabber = activeGrabbers[0];
                oldestGrabber?.ForceRelease();

                if (activeGrabbers.Count >= maximumGrabbers)
                {
                    return false;
                }
            }

            activeGrabbers.Add(grabber);
            Body?.Wake();
            Grabbed?.Invoke(grabber, pose);
            return true;
        }

        public void EndGrab(IApexGrabber grabber)
        {
            if (grabber == null || !activeGrabbers.Remove(grabber))
            {
                return;
            }

            Released?.Invoke(grabber);
        }

        public Vector3 GetWorldGripPosition(ApexGrabPose pose)
        {
            return transform.TransformPoint(pose.LocalPosition);
        }

        public Quaternion GetWorldGripRotation(ApexGrabPose pose)
        {
            return transform.rotation * pose.LocalRotation;
        }

        private Vector3 FindClosestPoint(Vector3 worldPosition)
        {
            Vector3 closestPoint = transform.position;
            float closestSqrDistance = float.PositiveInfinity;

            for (int i = 0; i < cachedColliders.Length; i++)
            {
                Collider candidate = cachedColliders[i];
                if (candidate == null || !candidate.enabled)
                {
                    continue;
                }

                Vector3 point = candidate.ClosestPoint(worldPosition);
                float sqrDistance = (point - worldPosition).sqrMagnitude;
                if (sqrDistance < closestSqrDistance)
                {
                    closestPoint = point;
                    closestSqrDistance = sqrDistance;
                }
            }

            return closestPoint;
        }

        private void CacheReferences()
        {
            if (cachedBody == null)
            {
                cachedBody = GetComponent<ApexBody>();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            maximumGrabbers = Mathf.Max(1, maximumGrabbers);
            minimumHandsToMove = Mathf.Clamp(minimumHandsToMove, 1, maximumGrabbers);
            CacheReferences();
            RefreshGrabData();
        }
#endif
    }
}
