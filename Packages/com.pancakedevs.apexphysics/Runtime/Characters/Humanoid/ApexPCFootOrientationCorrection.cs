using UnityEngine;
using UnityEngine.SceneManagement;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Applies the final procedural foot orientation after planted-foot IK and gait posing.
    /// The correction uses the motor's horizontal forward axis instead of assuming a model's
    /// local foot-bone axes. Ground targets, foot positions, and colliders are never changed.
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
        [SerializeField, Range(-20f, 20f)] private float plantedToePitchDegrees = 5f;
        [SerializeField, Range(0f, 15f)] private float movingToePitchExtraDegrees = 2f;
        [SerializeField, Range(0f, 1f)] private float correctionStrength = 1f;
        [SerializeField, Min(0.1f)] private float referenceSpeed = 3.5f;

        private Transform leftFoot;
        private Transform leftToes;
        private Transform rightFoot;
        private Transform rightToes;
        private float leftForwardSign = 1f;
        private float rightForwardSign = 1f;

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
            CacheBonesAndDirections();
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

            CorrectFoot(leftFoot, leftToes, leftForwardSign, pitch);
            CorrectFoot(rightFoot, rightToes, rightForwardSign, pitch);
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
                CacheBonesAndDirections();
            }
            else if (leftFoot == null || rightFoot == null)
            {
                CacheBonesAndDirections();
            }

            return targetAnimator != null && targetAnimator.isHuman &&
                   leftFoot != null && rightFoot != null;
        }

        private void CacheBonesAndDirections()
        {
            leftFoot = null;
            leftToes = null;
            rightFoot = null;
            rightToes = null;
            leftForwardSign = 1f;
            rightForwardSign = 1f;

            if (targetAnimator == null || !targetAnimator.isHuman)
            {
                return;
            }

            leftFoot = targetAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);
            leftToes = targetAnimator.GetBoneTransform(HumanBodyBones.LeftToes);
            rightFoot = targetAnimator.GetBoneTransform(HumanBodyBones.RightFoot);
            rightToes = targetAnimator.GetBoneTransform(HumanBodyBones.RightToes);

            if (character != null && character.MotorBody != null)
            {
                leftForwardSign = ResolveForwardSign(leftFoot, leftToes);
                rightForwardSign = ResolveForwardSign(rightFoot, rightToes);
            }
        }

        private float ResolveForwardSign(Transform foot, Transform toes)
        {
            if (foot == null || toes == null || character == null || character.MotorBody == null)
            {
                return 1f;
            }

            Vector3 toePlanar = Vector3.ProjectOnPlane(toes.position - foot.position, Vector3.up);
            Vector3 motorForward = Vector3.ProjectOnPlane(
                character.MotorBody.transform.forward,
                Vector3.up);
            if (toePlanar.sqrMagnitude < 0.000001f || motorForward.sqrMagnitude < 0.000001f)
            {
                return 1f;
            }

            return Vector3.Dot(toePlanar.normalized, motorForward.normalized) >= 0f ? 1f : -1f;
        }

        private void CorrectFoot(
            Transform foot,
            Transform toes,
            float forwardSign,
            float pitchDegrees)
        {
            if (foot == null || character == null || character.MotorBody == null)
            {
                return;
            }

            Vector3 currentForward;
            if (toes != null)
            {
                currentForward = toes.position - foot.position;
            }
            else
            {
                currentForward = character.MotorBody.transform.forward * forwardSign;
            }

            if (currentForward.sqrMagnitude < 0.000001f)
            {
                return;
            }

            Vector3 groundNormal = Vector3.up;
            Vector3 desiredPlanarForward = Vector3.ProjectOnPlane(
                character.MotorBody.transform.forward * forwardSign,
                groundNormal);
            if (desiredPlanarForward.sqrMagnitude < 0.000001f)
            {
                return;
            }

            float pitchRadians = pitchDegrees * Mathf.Deg2Rad;
            Vector3 desiredForward =
                desiredPlanarForward.normalized * Mathf.Cos(pitchRadians) +
                groundNormal * Mathf.Sin(pitchRadians);

            Quaternion fullCorrection = Quaternion.FromToRotation(
                currentForward.normalized,
                desiredForward.normalized);
            Quaternion targetRotation = fullCorrection * foot.rotation;
            foot.rotation = Quaternion.Slerp(
                foot.rotation,
                targetRotation,
                correctionStrength);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            plantedToePitchDegrees = Mathf.Clamp(plantedToePitchDegrees, -20f, 20f);
            movingToePitchExtraDegrees = Mathf.Clamp(movingToePitchExtraDegrees, 0f, 15f);
            correctionStrength = Mathf.Clamp01(correctionStrength);
            referenceSpeed = Mathf.Max(0.1f, referenceSpeed);
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
