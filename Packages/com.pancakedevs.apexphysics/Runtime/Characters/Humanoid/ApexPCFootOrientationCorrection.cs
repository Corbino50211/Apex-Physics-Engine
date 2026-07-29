using UnityEngine;
using UnityEngine.SceneManagement;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Owns the final procedural foot rotation after planted-foot IK and gait posing.
    /// It restores each avatar's original motor-relative foot orientation, aligns it to
    /// the sampled floor normal, and applies one small world-space toe-up pitch.
    /// Foot positions, IK targets, sole clearance, and colliders are never changed.
    /// </summary>
    [DefaultExecutionOrder(-255)]
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class ApexPCFootOrientationCorrection : MonoBehaviour
    {
        [SerializeField] private ApexPCPhysicalCharacter character;
        [SerializeField] private ApexPCProceduralGait proceduralGait;
        [SerializeField] private Animator targetAnimator;

        [Header("Foot Pitch")]
        [SerializeField, Range(-15f, 15f)] private float plantedToePitchDegrees = 4f;
        [SerializeField, Range(0f, 10f)] private float movingToePitchExtraDegrees = 2f;
        [SerializeField, Range(0f, 1f)] private float correctionStrength = 1f;
        [SerializeField, Min(0.1f)] private float referenceSpeed = 3.5f;

        [Header("Floor Alignment")]
        [SerializeField, Min(0.05f)] private float groundProbeHeight = 0.25f;
        [SerializeField, Min(0.05f)] private float groundProbeDistance = 0.65f;
        [SerializeField] private LayerMask groundLayers = ~0;

        private readonly RaycastHit[] groundHits = new RaycastHit[12];

        private Transform leftFoot;
        private Transform rightFoot;
        private Quaternion leftMotorRotationOffset = Quaternion.identity;
        private Quaternion rightMotorRotationOffset = Quaternion.identity;
        private bool rotationsCached;

        private void Awake()
        {
            Configure(character != null ? character : GetComponent<ApexPCPhysicalCharacter>());
        }

        private void OnEnable()
        {
            ResolveReferences();
        }

        public void Configure(ApexPCPhysicalCharacter owner)
        {
            character = owner;
            proceduralGait = owner != null ? owner.GetComponent<ApexPCProceduralGait>() : null;
            targetAnimator = owner != null && owner.Humanoid != null
                ? owner.Humanoid.TargetAnimator
                : null;
            CacheFeetAndBaseRotations();
        }

        private void LateUpdate()
        {
            if (!ResolveReferences() || correctionStrength <= 0f)
            {
                return;
            }

            if (proceduralGait != null && !proceduralGait.IsUsingProceduralGait)
            {
                return;
            }

            if (character.State != ApexPCCharacterState.Active &&
                character.State != ApexPCCharacterState.Staggered)
            {
                return;
            }

            float planarSpeed = Vector3.ProjectOnPlane(
                character.MotorBody.velocity,
                Vector3.up).magnitude;
            float speed01 = Mathf.Clamp01(planarSpeed / Mathf.Max(0.1f, referenceSpeed));
            float pitch = plantedToePitchDegrees + movingToePitchExtraDegrees * speed01;

            CorrectFoot(leftFoot, leftMotorRotationOffset, pitch);
            CorrectFoot(rightFoot, rightMotorRotationOffset, pitch);
        }

        private bool ResolveReferences()
        {
            if (character == null)
            {
                character = GetComponent<ApexPCPhysicalCharacter>();
            }

            if (character == null || character.Humanoid == null || character.MotorBody == null)
            {
                return false;
            }

            if (proceduralGait == null)
            {
                proceduralGait = character.GetComponent<ApexPCProceduralGait>();
            }

            Animator resolvedAnimator = character.Humanoid.TargetAnimator;
            if (targetAnimator != resolvedAnimator)
            {
                targetAnimator = resolvedAnimator;
                CacheFeetAndBaseRotations();
            }
            else if (!rotationsCached || leftFoot == null || rightFoot == null)
            {
                CacheFeetAndBaseRotations();
            }

            return rotationsCached && targetAnimator != null && targetAnimator.isHuman &&
                   leftFoot != null && rightFoot != null;
        }

        private void CacheFeetAndBaseRotations()
        {
            rotationsCached = false;
            leftFoot = null;
            rightFoot = null;
            leftMotorRotationOffset = Quaternion.identity;
            rightMotorRotationOffset = Quaternion.identity;

            if (targetAnimator == null || !targetAnimator.isHuman ||
                character == null || character.MotorBody == null)
            {
                return;
            }

            leftFoot = targetAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rightFoot = targetAnimator.GetBoneTransform(HumanBodyBones.RightFoot);
            if (leftFoot == null || rightFoot == null)
            {
                return;
            }

            Quaternion inverseMotorRotation = Quaternion.Inverse(character.MotorBody.rotation);
            leftMotorRotationOffset = inverseMotorRotation * leftFoot.rotation;
            rightMotorRotationOffset = inverseMotorRotation * rightFoot.rotation;
            rotationsCached = true;
        }

        private void CorrectFoot(
            Transform foot,
            Quaternion motorRotationOffset,
            float pitchDegrees)
        {
            if (foot == null || character == null || character.MotorBody == null)
            {
                return;
            }

            Rigidbody motor = character.MotorBody;
            Vector3 groundNormal = SampleGroundNormal(foot.position);
            Quaternion baseRotation = motor.rotation * motorRotationOffset;
            Quaternion slopeAlignment = Quaternion.FromToRotation(motor.transform.up, groundNormal);
            Quaternion slopeAlignedRotation = slopeAlignment * baseRotation;

            Vector3 pitchAxis = Vector3.ProjectOnPlane(motor.transform.right, groundNormal);
            if (pitchAxis.sqrMagnitude < 0.000001f)
            {
                pitchAxis = motor.transform.right;
            }
            pitchAxis.Normalize();

            Quaternion toePitch = Quaternion.AngleAxis(-pitchDegrees, pitchAxis);
            Quaternion targetRotation = toePitch * slopeAlignedRotation;
            foot.rotation = Quaternion.Slerp(
                foot.rotation,
                targetRotation,
                correctionStrength);
        }

        private Vector3 SampleGroundNormal(Vector3 footPosition)
        {
            Vector3 origin = footPosition + Vector3.up * groundProbeHeight;
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                groundHits,
                groundProbeHeight + groundProbeDistance,
                groundLayers,
                QueryTriggerInteraction.Ignore);

            float nearestDistance = float.PositiveInfinity;
            Vector3 nearestNormal = Vector3.up;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = groundHits[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (hit.distance < nearestDistance)
                {
                    nearestDistance = hit.distance;
                    nearestNormal = hit.normal;
                }
            }

            return nearestNormal.sqrMagnitude > 0.000001f
                ? nearestNormal.normalized
                : Vector3.up;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            plantedToePitchDegrees = Mathf.Clamp(plantedToePitchDegrees, -15f, 15f);
            movingToePitchExtraDegrees = Mathf.Clamp(movingToePitchExtraDegrees, 0f, 10f);
            correctionStrength = Mathf.Clamp01(correctionStrength);
            referenceSpeed = Mathf.Max(0.1f, referenceSpeed);
            groundProbeHeight = Mathf.Max(0.05f, groundProbeHeight);
            groundProbeDistance = Mathf.Max(0.05f, groundProbeDistance);
        }
#endif
    }

    /// <summary>
    /// Ensures older generated characters and runtime-spawned characters receive the final
    /// foot-orientation correction without requiring another generated hierarchy component.
    /// </summary>
    internal sealed class ApexPCFootOrientationBootstrap : MonoBehaviour
    {
        private const float ScanInterval = 0.5f;
        private float nextScanTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateBootstrap()
        {
            ApexPCFootOrientationBootstrap existing =
                Object.FindFirstObjectByType<ApexPCFootOrientationBootstrap>();
            if (existing != null)
            {
                return;
            }

            GameObject bootstrapObject = new GameObject("Apex PC Foot Orientation Bootstrap");
            bootstrapObject.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(bootstrapObject);
            bootstrapObject.AddComponent<ApexPCFootOrientationBootstrap>();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            InstallCorrections();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void Update()
        {
            if (Time.unscaledTime < nextScanTime)
            {
                return;
            }

            nextScanTime = Time.unscaledTime + ScanInterval;
            InstallCorrections();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            InstallCorrections();
        }

        private static void InstallCorrections()
        {
            ApexPCPhysicalCharacter[] characters = Object.FindObjectsByType<ApexPCPhysicalCharacter>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < characters.Length; i++)
            {
                ApexPCPhysicalCharacter character = characters[i];
                if (character == null ||
                    character.GetComponent<ApexPCFootOrientationCorrection>() != null)
                {
                    continue;
                }

                ApexPCFootOrientationCorrection correction =
                    character.gameObject.AddComponent<ApexPCFootOrientationCorrection>();
                correction.hideFlags = HideFlags.HideInInspector;
                correction.Configure(character);
            }
        }
    }
}
