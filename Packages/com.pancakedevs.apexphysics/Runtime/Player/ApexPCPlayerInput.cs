using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Optional New Input System desktop adapter for the Apex PC player rig.
    /// The motor, look controller, and interaction layer remain input-agnostic.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ApexPCPlayerInput : MonoBehaviour
    {
        [SerializeField] private ApexPCPlayerMotor motor;
        [SerializeField] private ApexPCPlayerLook look;
        [SerializeField] private ApexGrabber grabber;
        [SerializeField] private ApexPCPlayerInteraction interaction;
        [SerializeField] private bool escapeUnlocksCursor = true;

        private bool warnedMissingInputSystem;

        private void Reset()
        {
            motor = GetComponent<ApexPCPlayerMotor>();
            look = GetComponent<ApexPCPlayerLook>();
            grabber = GetComponentInChildren<ApexGrabber>(true);
            interaction = GetComponent<ApexPCPlayerInteraction>();
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

            if (grabber == null)
            {
                grabber = GetComponentInChildren<ApexGrabber>(true);
            }

            if (interaction == null)
            {
                interaction = GetComponent<ApexPCPlayerInteraction>();
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

                if (Cursor.lockState == CursorLockMode.Locked)
                {
                    float scroll = mouse.scroll.ReadValue().y;
                    if (Mathf.Abs(scroll) > Mathf.Epsilon)
                    {
                        interaction?.AdjustHoldDistance(scroll > 0f ? 1f : -1f);
                    }

                    if (mouse.leftButton.wasPressedThisFrame)
                    {
                        interaction?.ThrowHeld();
                    }
                }
                else if (mouse.leftButton.wasPressedThisFrame)
                {
                    look?.SetCursorLocked(true);
                }
            }

            if (keyboard.eKey.wasPressedThisFrame)
            {
                if (interaction != null)
                {
                    interaction.TryInteract();
                }
                else if (grabber != null)
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
                    "ApexPCPlayerInput requires the Unity Input System. The player rig can still be driven through its public input methods.",
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
