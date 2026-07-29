using UnityEngine;
using UnityEngine.SceneManagement;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Repairs common imported-avatar problems after binding: tiny or enormous model scale,
    /// visible debug hand meshes, and invalid calibration values. Existing bound avatars are
    /// upgraded automatically when a scene loads.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public sealed class ApexVRAvatarCalibration : MonoBehaviour
    {
        [SerializeField] private bool calibrateScale = true;
        [SerializeField, Range(0.5f, 1.15f)] private float targetHeadHeightRatio = 0.96f;
        [SerializeField, Min(0.01f)] private float minimumScaleMultiplier = 0.1f;
        [SerializeField, Min(1f)] private float maximumScaleMultiplier = 10f;
        [SerializeField] private bool hideDebugHandVisuals = true;

        private ApexPhysicalVRAvatar avatar;
        private bool calibrated;

        private void Awake()
        {
            avatar = GetComponent<ApexPhysicalVRAvatar>();
        }

        private void Start()
        {
            Calibrate();
        }

        [ContextMenu("Recalibrate Apex VR Avatar")]
        public void Calibrate()
        {
            if (calibrated)
            {
                return;
            }

            if (avatar == null)
            {
                avatar = GetComponent<ApexPhysicalVRAvatar>();
            }

            if (avatar == null || avatar.Animator == null || avatar.PhysicalBody == null)
            {
                return;
            }

            if (calibrateScale)
            {
                CalibrateAvatarScale();
            }

            if (hideDebugHandVisuals)
            {
                HideDebugHands();
            }

            calibrated = true;
        }

        private void CalibrateAvatarScale()
        {
            Animator animator = avatar.Animator;
            Transform headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            Transform trackedHead = avatar.PhysicalBody.Head;

            if (headBone == null || trackedHead == null)
            {
                return;
            }

            float feetY = transform.position.y;
            int footCount = 0;
            if (leftFoot != null)
            {
                feetY += leftFoot.position.y;
                footCount++;
            }
            if (rightFoot != null)
            {
                feetY += rightFoot.position.y;
                footCount++;
            }
            if (footCount > 0)
            {
                feetY = (feetY - transform.position.y) / footCount;
            }

            float currentHeight = headBone.position.y - feetY;
            float desiredHeight = (trackedHead.position.y - avatar.PhysicalBody.transform.position.y) *
                                  targetHeadHeightRatio;

            if (!float.IsFinite(currentHeight) || !float.IsFinite(desiredHeight) ||
                currentHeight < 0.05f || desiredHeight < 0.25f)
            {
                return;
            }

            float multiplier = Mathf.Clamp(
                desiredHeight / currentHeight,
                minimumScaleMultiplier,
                maximumScaleMultiplier);

            if (Mathf.Abs(multiplier - 1f) < 0.02f)
            {
                return;
            }

            transform.localScale *= multiplier;
        }

        private void HideDebugHands()
        {
            ApexPhysicalOpenXRHand[] hands =
                avatar.PhysicalBody.GetComponentsInChildren<ApexPhysicalOpenXRHand>(true);

            foreach (ApexPhysicalOpenXRHand hand in hands)
            {
                Renderer[] renderers = hand.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer renderer in renderers)
                {
                    string lower = renderer.name.ToLowerInvariant();
                    if (lower.Contains("hand visual") || lower.Contains("debug") || lower.Contains("sphere"))
                    {
                        renderer.enabled = false;
                    }
                }
            }
        }
    }

    internal static class ApexVRAvatarCalibrationBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            InstallInScene(SceneManager.GetActiveScene());
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            InstallInScene(scene);
        }

        private static void InstallInScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                ApexPhysicalVRAvatar[] avatars =
                    root.GetComponentsInChildren<ApexPhysicalVRAvatar>(true);
                foreach (ApexPhysicalVRAvatar avatar in avatars)
                {
                    if (avatar.GetComponent<ApexVRAvatarCalibration>() == null)
                    {
                        avatar.gameObject.AddComponent<ApexVRAvatarCalibration>();
                    }
                }
            }
        }
    }
}
