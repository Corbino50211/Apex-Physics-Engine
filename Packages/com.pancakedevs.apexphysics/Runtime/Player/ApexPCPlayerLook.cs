using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>Input-agnostic yaw and pitch controller for the desktop player camera.</summary>
    [DisallowMultipleComponent]
    public sealed class ApexPCPlayerLook : MonoBehaviour
    {
        [SerializeField] private Transform yawRoot;
        [SerializeField] private Transform pitchRoot;
        [SerializeField, Min(0f)] private float sensitivity = 0.12f;
        [SerializeField, Range(1f, 89f)] private float maximumPitch = 85f;
        [SerializeField] private bool lockCursorOnEnable = true;

        private Vector2 lookInput;
        private float pitch;

        public float Pitch => pitch;

        private void Reset()
        {
            yawRoot = transform;
            pitchRoot = transform;
        }

        private void OnEnable()
        {
            if (lockCursorOnEnable)
            {
                SetCursorLocked(true);
            }
        }

        private void OnDisable()
        {
            lookInput = Vector2.zero;
        }

        private void Update()
        {
            Transform yaw = yawRoot != null ? yawRoot : transform;
            Transform pitchTransform = pitchRoot != null ? pitchRoot : transform;

            yaw.Rotate(Vector3.up, lookInput.x * sensitivity, Space.World);
            pitch = Mathf.Clamp(pitch - lookInput.y * sensitivity, -maximumPitch, maximumPitch);
            Vector3 angles = pitchTransform.localEulerAngles;
            angles.x = pitch;
            pitchTransform.localEulerAngles = angles;
        }

        public void SetLookInput(Vector2 value)
        {
            lookInput = value;
        }

        public void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            sensitivity = Mathf.Max(0f, sensitivity);
            maximumPitch = Mathf.Clamp(maximumPitch, 1f, 89f);
        }
#endif
    }
}
