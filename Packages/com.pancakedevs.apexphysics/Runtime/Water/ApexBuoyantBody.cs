using System;
using System.Collections.Generic;
using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Applies point-sampled buoyancy, water drag, and currents to any Rigidbody.
    /// Works with ordinary physics props, ApexBody objects, destructible chunks,
    /// crates, boats, and ragdoll bodies.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("Apex Physics Engine/Water/Buoyant Body")]
    public sealed class ApexBuoyantBody : MonoBehaviour, IApexWaterReactive
    {
        [Serializable]
        public struct BuoyancyPoint
        {
            public Vector3 localOffset;
        }

        [Header("Buoyancy Points")]
        [SerializeField] private BuoyancyPoint[] points =
        {
            new BuoyancyPoint { localOffset = new Vector3(0.5f, 0f, 0.5f) },
            new BuoyancyPoint { localOffset = new Vector3(-0.5f, 0f, 0.5f) },
            new BuoyancyPoint { localOffset = new Vector3(0.5f, 0f, -0.5f) },
            new BuoyancyPoint { localOffset = new Vector3(-0.5f, 0f, -0.5f) }
        };

        [Header("Buoyancy")]
        [SerializeField, Range(0.05f, 5f)] private float relativeDensity = 0.6f;
        [SerializeField, Min(0f)] private float buoyancyAcceleration = 10f;
        [SerializeField, Min(0.01f)] private float submersionResponse = 3f;
        [SerializeField, Min(0f)] private float verticalDamping = 1.5f;

        [Header("Water Drag")]
        [SerializeField, Min(0f)] private float waterLinearDamping = 1.5f;
        [SerializeField, Min(0f)] private float waterAngularDamping = 1f;
        [SerializeField, Min(0f)] private float currentMultiplier = 1f;

        private readonly List<ApexWaterVolume> activeVolumes = new List<ApexWaterVolume>();
        private Rigidbody body;
        private float defaultLinearDamping;
        private float defaultAngularDamping;

        public bool IsInWater => activeVolumes.Count > 0;
        public float SubmersionFraction { get; private set; }
        public IReadOnlyList<ApexWaterVolume> ActiveVolumes => activeVolumes;

        private void Awake()
        {
            CacheBody();
        }

        private void OnDisable()
        {
            RestoreDamping();
            activeVolumes.Clear();
            SubmersionFraction = 0f;
        }

        public void OnApexWaterEnter(ApexWaterVolume volume)
        {
            if (volume != null && !activeVolumes.Contains(volume))
            {
                activeVolumes.Add(volume);
            }
        }

        public void OnApexWaterExit(ApexWaterVolume volume)
        {
            activeVolumes.Remove(volume);
            if (activeVolumes.Count == 0)
            {
                RestoreDamping();
            }
        }

        private void FixedUpdate()
        {
            CacheBody();
            RemoveMissingVolumes();
            if (activeVolumes.Count == 0 || body == null)
            {
                SubmersionFraction = 0f;
                return;
            }

            int pointCount = points != null && points.Length > 0 ? points.Length : 1;
            int submerged = 0;

            for (int i = 0; i < pointCount; i++)
            {
                Vector3 worldPoint = points != null && points.Length > 0
                    ? transform.TransformPoint(points[i].localOffset)
                    : body.worldCenterOfMass;
                ApexWaterVolume volume = FindBestActiveVolume(worldPoint);
                if (volume == null)
                {
                    continue;
                }

                float submersion = volume.GetWaterHeight(worldPoint) - worldPoint.y;
                if (submersion <= 0f)
                {
                    continue;
                }

                submerged++;
                float depthFactor = Mathf.Clamp01(submersion * submersionResponse);
                float acceleration = buoyancyAcceleration * depthFactor /
                                     Mathf.Max(0.05f, relativeDensity);
                body.AddForceAtPosition(
                    Vector3.up * acceleration,
                    worldPoint,
                    ForceMode.Acceleration);

                float pointVerticalVelocity = body.GetPointVelocity(worldPoint).y;
                body.AddForceAtPosition(
                    Vector3.down * pointVerticalVelocity * verticalDamping * depthFactor,
                    worldPoint,
                    ForceMode.Acceleration);

                Vector3 current = volume.GetCurrent(worldPoint) * currentMultiplier;
                if (current.sqrMagnitude > 0.0001f)
                {
                    body.AddForceAtPosition(current, worldPoint, ForceMode.Acceleration);
                }
            }

            SubmersionFraction = submerged / (float)pointCount;
            body.linearDamping = Mathf.Lerp(
                defaultLinearDamping,
                waterLinearDamping,
                SubmersionFraction);
            body.angularDamping = Mathf.Lerp(
                defaultAngularDamping,
                waterAngularDamping,
                SubmersionFraction);
        }

        public void GeneratePointsFromColliders(bool includeCenter = false)
        {
            Collider[] colliders = GetComponentsInChildren<Collider>();
            if (colliders.Length == 0)
            {
                points = new[] { new BuoyancyPoint { localOffset = Vector3.zero } };
                return;
            }

            Bounds bounds = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++)
            {
                if (!colliders[i].isTrigger)
                {
                    bounds.Encapsulate(colliders[i].bounds);
                }
            }

            Vector3 minimum = transform.InverseTransformPoint(bounds.min);
            Vector3 maximum = transform.InverseTransformPoint(bounds.max);
            float sampleY = Mathf.Lerp(minimum.y, maximum.y, 0.35f);
            List<BuoyancyPoint> generated = new List<BuoyancyPoint>(includeCenter ? 5 : 4)
            {
                new BuoyancyPoint { localOffset = new Vector3(minimum.x, sampleY, minimum.z) },
                new BuoyancyPoint { localOffset = new Vector3(maximum.x, sampleY, minimum.z) },
                new BuoyancyPoint { localOffset = new Vector3(minimum.x, sampleY, maximum.z) },
                new BuoyancyPoint { localOffset = new Vector3(maximum.x, sampleY, maximum.z) }
            };
            if (includeCenter)
            {
                generated.Add(new BuoyancyPoint
                {
                    localOffset = new Vector3(
                        (minimum.x + maximum.x) * 0.5f,
                        sampleY,
                        (minimum.z + maximum.z) * 0.5f)
                });
            }
            points = generated.ToArray();
        }

        private ApexWaterVolume FindBestActiveVolume(Vector3 worldPoint)
        {
            ApexWaterVolume best = null;
            float highestSurface = float.NegativeInfinity;
            for (int i = 0; i < activeVolumes.Count; i++)
            {
                ApexWaterVolume volume = activeVolumes[i];
                if (volume == null || !volume.ContainsHorizontal(worldPoint) ||
                    worldPoint.y < volume.GetFloorHeight(worldPoint))
                {
                    continue;
                }

                float surface = volume.GetWaterHeight(worldPoint);
                if (surface > highestSurface)
                {
                    highestSurface = surface;
                    best = volume;
                }
            }
            return best;
        }

        private void CacheBody()
        {
            if (body != null)
            {
                return;
            }

            body = GetComponent<Rigidbody>();
            if (body != null)
            {
                defaultLinearDamping = body.linearDamping;
                defaultAngularDamping = body.angularDamping;
            }
        }

        private void RestoreDamping()
        {
            if (body == null)
            {
                return;
            }

            body.linearDamping = defaultLinearDamping;
            body.angularDamping = defaultAngularDamping;
        }

        private void RemoveMissingVolumes()
        {
            for (int i = activeVolumes.Count - 1; i >= 0; i--)
            {
                if (activeVolumes[i] == null || !activeVolumes[i].isActiveAndEnabled)
                {
                    activeVolumes.RemoveAt(i);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (points == null)
            {
                return;
            }

            Gizmos.color = Color.cyan;
            for (int i = 0; i < points.Length; i++)
            {
                Gizmos.DrawSphere(transform.TransformPoint(points[i].localOffset), 0.08f);
            }
        }
    }
}
