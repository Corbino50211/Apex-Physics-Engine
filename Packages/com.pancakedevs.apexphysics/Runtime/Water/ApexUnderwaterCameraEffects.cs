using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Optional camera-side underwater fog. RenderSettings fog is global, so use one
    /// instance for the local player camera unless a render-pipeline-specific effect
    /// replaces it.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Apex Physics Engine/Water/Underwater Camera Effects")]
    public sealed class ApexUnderwaterCameraEffects : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Color underwaterFogColor = new Color(0.05f, 0.2f, 0.3f);
        [SerializeField, Min(0f)] private float underwaterFogDensity = 0.08f;
        [SerializeField] private bool changeCameraBackground = true;

        private bool underwater;
        private bool previousFogEnabled;
        private FogMode previousFogMode;
        private Color previousFogColor;
        private float previousFogDensity;
        private Color previousBackgroundColor;

        public bool IsUnderwater => underwater;

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = GetComponent<Camera>();
            }
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }

        private void LateUpdate()
        {
            Vector3 position = targetCamera != null
                ? targetCamera.transform.position
                : transform.position;
            SetUnderwater(ApexWaterManager.IsUnderwater(position));
        }

        private void OnDisable()
        {
            SetUnderwater(false);
        }

        private void SetUnderwater(bool value)
        {
            if (underwater == value)
            {
                return;
            }

            underwater = value;
            if (value)
            {
                previousFogEnabled = RenderSettings.fog;
                previousFogMode = RenderSettings.fogMode;
                previousFogColor = RenderSettings.fogColor;
                previousFogDensity = RenderSettings.fogDensity;
                if (targetCamera != null)
                {
                    previousBackgroundColor = targetCamera.backgroundColor;
                }

                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Exponential;
                RenderSettings.fogColor = underwaterFogColor;
                RenderSettings.fogDensity = underwaterFogDensity;
                if (changeCameraBackground && targetCamera != null)
                {
                    targetCamera.backgroundColor = underwaterFogColor;
                }
            }
            else
            {
                RenderSettings.fog = previousFogEnabled;
                RenderSettings.fogMode = previousFogMode;
                RenderSettings.fogColor = previousFogColor;
                RenderSettings.fogDensity = previousFogDensity;
                if (changeCameraBackground && targetCamera != null)
                {
                    targetCamera.backgroundColor = previousBackgroundColor;
                }
            }
        }
    }
}
