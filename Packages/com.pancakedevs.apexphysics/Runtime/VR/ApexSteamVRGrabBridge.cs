using System;
using System.Reflection;
using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Optional reflection bridge between Valve's Interaction System Hand component
    /// and ApexGrabber. Reflection keeps SteamVR an optional project dependency.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ApexSteamVRGrabBridge : MonoBehaviour
    {
        [SerializeField] private Component steamVrHand;
        [SerializeField] private ApexGrabber grabber;

        private object gripAction;
        private object inputSource;
        private MethodInfo getStateMethod;
        private bool previousGrip;
        private bool initialized;
        private bool warned;

        private void Awake()
        {
            Initialize();
        }

        private void Update()
        {
            if (!initialized && !Initialize())
            {
                return;
            }

            bool gripHeld;
            try
            {
                gripHeld = (bool)getStateMethod.Invoke(gripAction, new[] { inputSource });
            }
            catch (Exception exception)
            {
                if (!warned)
                {
                    warned = true;
                    Debug.LogWarning(
                        "Apex could not read the SteamVR grip action. Open Window > SteamVR Input, copy the example JSON files, then Save and Generate. " +
                        exception.GetBaseException().Message,
                        this);
                }
                return;
            }

            if (gripHeld && !previousGrip)
            {
                grabber?.TryGrabClosest();
            }
            else if (!gripHeld && previousGrip)
            {
                grabber?.Release();
            }

            previousGrip = gripHeld;
        }

        private void OnDisable()
        {
            grabber?.ForceRelease();
            previousGrip = false;
        }

        private bool Initialize()
        {
            initialized = false;
            if (steamVrHand == null || grabber == null)
            {
                WarnOnce("ApexSteamVRGrabBridge is missing its Valve Hand or ApexGrabber reference.");
                return false;
            }

            Type handType = steamVrHand.GetType();
            gripAction = ReadMember(handType, steamVrHand, "grabGripAction");
            inputSource = ReadMember(handType, steamVrHand, "handType");

            if (gripAction == null || inputSource == null)
            {
                WarnOnce(
                    "Apex found the SteamVR Hand component but could not locate grabGripAction/handType. " +
                    "Make sure the SteamVR Interaction System sample and generated actions are installed.");
                return false;
            }

            getStateMethod = gripAction.GetType().GetMethod(
                "GetState",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { inputSource.GetType() },
                null);

            if (getStateMethod == null)
            {
                WarnOnce("Apex could not locate SteamVR_Action_Boolean.GetState for this SteamVR version.");
                return false;
            }

            initialized = true;
            warned = false;
            return true;
        }

        private void WarnOnce(string message)
        {
            if (warned)
            {
                return;
            }

            warned = true;
            Debug.LogWarning(message, this);
        }

        private static object ReadMember(Type type, object instance, string memberName)
        {
            FieldInfo field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                return field.GetValue(instance);
            }

            PropertyInfo property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return property != null ? property.GetValue(instance) : null;
        }

#if UNITY_EDITOR
        public void EditorConfigure(Component hand, ApexGrabber handGrabber)
        {
            steamVrHand = hand;
            grabber = handGrabber;
        }
#endif
    }
}
