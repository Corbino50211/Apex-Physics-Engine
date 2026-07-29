using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Optional legacy Input Manager adapter for quick desktop testing. New Input
    /// System, AI, networking, and VR code should call ApexSwimmer's input API directly.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ApexSwimmer))]
    [AddComponentMenu("Apex Physics Engine/Water/Legacy Swim Input")]
    public sealed class ApexLegacySwimInput : MonoBehaviour
    {
        [SerializeField] private string horizontalAxis = "Horizontal";
        [SerializeField] private string verticalAxis = "Vertical";
        [SerializeField] private KeyCode ascendKey = KeyCode.Space;
        [SerializeField] private KeyCode descendKey = KeyCode.LeftControl;
        [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;

        private ApexSwimmer swimmer;
        private bool warnedUnavailable;

        private void Awake()
        {
            swimmer = GetComponent<ApexSwimmer>();
        }

        private void Update()
        {
            if (swimmer == null)
            {
                return;
            }

#if ENABLE_LEGACY_INPUT_MANAGER
            Vector2 movement = new Vector2(
                Input.GetAxisRaw(horizontalAxis),
                Input.GetAxisRaw(verticalAxis));
            float vertical = 0f;
            if (Input.GetKey(ascendKey))
            {
                vertical += 1f;
            }
            if (Input.GetKey(descendKey))
            {
                vertical -= 1f;
            }
            swimmer.SetInput(movement, vertical, Input.GetKey(sprintKey));
#else
            if (!warnedUnavailable)
            {
                warnedUnavailable = true;
                Debug.LogWarning(
                    "ApexLegacySwimInput requires the Legacy Input Manager. " +
                    "Feed ApexSwimmer from your Input System actions instead.",
                    this);
            }
#endif
        }

        private void OnDisable()
        {
            swimmer?.ClearInput();
        }
    }
}
