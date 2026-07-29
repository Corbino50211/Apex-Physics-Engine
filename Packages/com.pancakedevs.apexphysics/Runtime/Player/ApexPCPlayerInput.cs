using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Optional New Input System desktop adapter for ApexPCPlayerMotor and ApexPCPlayerLook.
    /// The motor and look controller remain input-agnostic.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ApexPCPlayerInput : MonoBehaviour
    {
        [SerializeField] private ApexPCPlayerMotor motor;
        [SerializeField] private ApexPCPlayerLook look;
        [SerializeField] private ApexGrabber grabber;
        [SerializeField] private bool escapeUnlocksCursor = true;

        private bool warnedMissingInputSystem;

        private void Reset()
        {
            motor = GetComponent<ApexPCPlayerMotor>();
            look = GetComponent<ApexPCPlayerLook>();
            grabber = GetComponentInChildren<ApexGrabber>(true);
        }

        private void Awake()
        {
            if (motor == null)
            {
                motor = GetComponent<ApexPCPlayerMotor>();
            }

            if (look == null)
            {
                look = GetComponent<ApexPCPlayerLook>();
            }
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (keyboard == null)
            {
                motor?.ClearInput();
                look?.SetLookInput(Vector2.zero);
                return;
            }

            Vector2 move = Vector2.zero;
            if (keyboard.wKey.isPressed) move.y += 1f;
            if (keyboard.sKey.isPressed) move.y -= 1f;
            if (keyboard.dKey.isPressed) move.x += 1f;
            if (keyboard.aKey.isPressed) move.x -= 1f;

            motor?.SetMoveInput(Vector2.ClampMagnitude(move, 1f));
            motor?.SetSprint(keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);

            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                motor?.QueueJump();
            }

            if (mouse != null)
            {
                look?.SetLookInput(Cursor.lockState == CursorLockMode.Locked
                    ? mouse.delta.ReadValue()
                    : Vector2.zero);

                if (mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
                {
                    look?.SetCursorLocked(true);
                }
            }

            if (keyboard.eKey.wasPressedThisFrame && grabber != null)
            {
                if (grabber.IsHolding)
                {
                    grabber.Release();
                }
                else
                {
                    grabber.TryGrabClosest();
                }
            }

            if (escapeUnlocksCursor && keyboard.escapeKey.wasPressedThisFrame)
            {
                look?.SetCursorLocked(false);
            }
#else
            motor?.ClearInput();
            look?.SetLookInput(Vector2.zero);
            if (!warnedMissingInputSystem)
            {
                warnedMissingInputSystem = true;
                Debug.LogWarning(
                    "ApexPCPlayerInput requires the Unity Input System. The player motor can still be driven through its public input methods.",
                    this);
            }
#endif
        }

        private void OnDisable()
        {
            motor?.ClearInput();
            look?.SetLookInput(Vector2.zero);
        }
    }
}
