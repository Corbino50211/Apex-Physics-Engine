using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR;

namespace PancakeDevs.ApexPhysics.Editor
{
    internal static class ApexVRPlayerMenu
    {
        private const string OpenXRPath =
            "Apex Physics Engine/Player/Create OpenXR VR Player Rig";
        private const string SteamVRPath =
            "Apex Physics Engine/Player/Create SteamVR Player Rig From Valve Prefab";
        private const string ValvePlayerPrefabPath =
            "Assets/SteamVR/InteractionSystem/Core/Prefabs/Player.prefab";

        [MenuItem(OpenXRPath, false, 120)]
        private static void CreateOpenXRRig()
        {
            CreateGenericOpenXRRig();
        }

        [MenuItem(SteamVRPath, false, 121)]
        private static void CreateSteamVRRig()
        {
            GameObject valvePrefab = FindValvePlayerPrefab();
            if (valvePrefab == null)
            {
                EditorUtility.DisplayDialog(
                    "SteamVR Player Prefab Not Found",
                    "Apex could not find Valve's Interaction System Player prefab.\n\n" +
                    "Import the SteamVR Interaction System, then open Window > SteamVR Input, " +
                    "copy the example JSON files, and click Save and Generate.",
                    "OK");
                return;
            }

            if (CountMissingScripts(valvePrefab) > 0)
            {
                EditorUtility.DisplayDialog(
                    "SteamVR Prefab Has Missing Scripts",
                    "Valve's Player prefab contains missing scripts, so Apex stopped instead of creating a broken rig.\n\n" +
                    "Reimport SteamVR, open Window > SteamVR Input, copy the example JSON files, " +
                    "click Save and Generate, then restart Unity.",
                    "OK");
                return;
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create Apex SteamVR Player Rig");

            GameObject root = CreatePhysicsRoot("Apex SteamVR Player");
            CapsuleCollider capsule = root.GetComponent<CapsuleCollider>();
            ApexVRPlayerRig rig = Undo.AddComponent<ApexVRPlayerRig>(root);
            ApexVRPlayerMotor motor = Undo.AddComponent<ApexVRPlayerMotor>(root);
            ApexVRInput input = Undo.AddComponent<ApexVRInput>(root);

            GameObject valvePlayer = PrefabUtility.InstantiatePrefab(valvePrefab) as GameObject;
            if (valvePlayer == null)
            {
                Undo.DestroyObjectImmediate(root);
                EditorUtility.DisplayDialog(
                    "SteamVR Rig Creation Failed",
                    "Unity could not instantiate Valve's Player prefab.",
                    "OK");
                return;
            }

            Undo.RegisterCreatedObjectUndo(valvePlayer, "Instantiate Valve SteamVR Player");
            valvePlayer.name = "Valve SteamVR Player Tracking";
            valvePlayer.transform.SetParent(root.transform, false);
            valvePlayer.transform.localPosition = Vector3.zero;
            valvePlayer.transform.localRotation = Quaternion.identity;

            Component valvePlayerComponent = FindComponentByFullName(
                valvePlayer,
                "Valve.VR.InteractionSystem.Player");
            if (valvePlayerComponent == null ||
                !TryResolveValveReferences(
                    valvePlayerComponent,
                    valvePlayer,
                    out Transform head,
                    out Component leftValveHand,
                    out Component rightValveHand))
            {
                Undo.DestroyObjectImmediate(root);
                EditorUtility.DisplayDialog(
                    "SteamVR Player References Missing",
                    "Apex found Valve's prefab but could not resolve its HMD and two Hand components. " +
                    "Reimport the SteamVR Interaction System sample and generate its input actions.",
                    "OK");
                return;
            }

            Transform leftTarget = leftValveHand.transform;
            Transform rightTarget = rightValveHand.transform;

            ApexVRPhysicalHand leftHand = CreatePhysicalHand(
                root.transform,
                "Apex Left Physical Hand",
                leftTarget,
                capsule,
                false);
            ApexVRPhysicalHand rightHand = CreatePhysicalHand(
                root.transform,
                "Apex Right Physical Hand",
                rightTarget,
                capsule,
                false);

            ApexSteamVRGrabBridge leftBridge = Undo.AddComponent<ApexSteamVRGrabBridge>(leftHand.gameObject);
            leftBridge.EditorConfigure(leftValveHand, leftHand.Grabber);
            ApexSteamVRGrabBridge rightBridge = Undo.AddComponent<ApexSteamVRGrabBridge>(rightHand.gameObject);
            rightBridge.EditorConfigure(rightValveHand, rightHand.Grabber);

            rig.EditorConfigure(ApexVRBackend.SteamVROpenVR, head, leftHand, rightHand);
            motor.EditorConfigure(head);
            // SteamVR grip is handled by ApexSteamVRGrabBridge. Unity XR input remains available
            // for movement, snap turning, and jump when the OpenVR loader exposes those axes.
            input.EditorConfigure(motor, null, null);

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(root.scene);
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log(
                "Created Apex SteamVR Player from Valve's Interaction System Player prefab. " +
                "Tracking and controller models come from Valve; Apex adds the Rigidbody body, physical hand proxies, and Apex grabbing.",
                root);
        }

        private static void CreateGenericOpenXRRig()
        {
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create Apex OpenXR Player Rig");

            GameObject root = CreatePhysicsRoot("Apex OpenXR VR Player");
            CapsuleCollider capsule = root.GetComponent<CapsuleCollider>();
            ApexVRPlayerRig rig = Undo.AddComponent<ApexVRPlayerRig>(root);
            ApexVRPlayerMotor motor = Undo.AddComponent<ApexVRPlayerMotor>(root);
            ApexVRInput input = Undo.AddComponent<ApexVRInput>(root);

            Transform trackingSpace = CreateChild(root.transform, "Tracking Space");
            Transform head = CreateTrackedTarget(
                trackingSpace,
                "Tracked Head",
                XRNode.CenterEye,
                new Vector3(0f, 1.7f, 0f));

            Camera camera = Undo.AddComponent<Camera>(head.gameObject);
            camera.tag = "MainCamera";
            Undo.AddComponent<AudioListener>(head.gameObject);

            Transform leftTarget = CreateTrackedTarget(
                trackingSpace,
                "Left Hand Target",
                XRNode.LeftHand,
                new Vector3(-0.25f, 1.3f, 0.35f));
            Transform rightTarget = CreateTrackedTarget(
                trackingSpace,
                "Right Hand Target",
                XRNode.RightHand,
                new Vector3(0.25f, 1.3f, 0.35f));

            ApexVRPhysicalHand leftHand = CreatePhysicalHand(
                root.transform,
                "Left Physical Hand",
                leftTarget,
                capsule,
                true);
            ApexVRPhysicalHand rightHand = CreatePhysicalHand(
                root.transform,
                "Right Physical Hand",
                rightTarget,
                capsule,
                true);

            rig.EditorConfigure(ApexVRBackend.OpenXR, head, leftHand, rightHand);
            motor.EditorConfigure(head);
            input.EditorConfigure(motor, leftHand.Grabber, rightHand.Grabber);

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(root.scene);
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log(
                "Created Apex OpenXR VR Player. Enable OpenXR in XR Plug-in Management before Play Mode.",
                root);
        }

        private static GameObject CreatePhysicsRoot(string name)
        {
            GameObject root = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(root, "Create Apex VR Player Rig");
            root.transform.position = FindSpawnPosition();

            CapsuleCollider capsule = Undo.AddComponent<CapsuleCollider>(root);
            capsule.radius = 0.3f;
            capsule.height = 1.8f;
            capsule.center = new Vector3(0f, 0.9f, 0f);

            Rigidbody body = Undo.AddComponent<Rigidbody>(root);
            body.mass = 80f;
            body.useGravity = true;
            body.isKinematic = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            return root;
        }

        private static GameObject FindValvePlayerPrefab()
        {
            GameObject exact = AssetDatabase.LoadAssetAtPath<GameObject>(ValvePlayerPrefabPath);
            if (exact != null)
            {
                return exact;
            }

            string[] guids = AssetDatabase.FindAssets("Player t:Prefab", new[] { "Assets/SteamVR" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                string normalized = path.Replace('\\', '/');
                if (!normalized.Contains("InteractionSystem/Core/Prefabs", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                GameObject candidate = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (candidate != null &&
                    FindComponentByFullName(candidate, "Valve.VR.InteractionSystem.Player") != null)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static bool TryResolveValveReferences(
            Component playerComponent,
            GameObject playerRoot,
            out Transform head,
            out Component leftHand,
            out Component rightHand)
        {
            head = ReadMember(playerComponent, "hmdTransform") as Transform;
            leftHand = null;
            rightHand = null;

            object handsValue = ReadMember(playerComponent, "hands");
            if (handsValue is Array handsArray)
            {
                for (int i = 0; i < handsArray.Length; i++)
                {
                    Component hand = handsArray.GetValue(i) as Component;
                    ClassifyValveHand(hand, ref leftHand, ref rightHand);
                }
            }

            if (leftHand == null || rightHand == null)
            {
                Component[] components = playerRoot.GetComponentsInChildren<Component>(true);
                for (int i = 0; i < components.Length; i++)
                {
                    Component component = components[i];
                    if (component != null &&
                        component.GetType().FullName == "Valve.VR.InteractionSystem.Hand")
                    {
                        ClassifyValveHand(component, ref leftHand, ref rightHand);
                    }
                }
            }

            if (head == null)
            {
                Camera camera = playerRoot.GetComponentInChildren<Camera>(true);
                head = camera != null ? camera.transform : null;
            }

            return head != null && leftHand != null && rightHand != null;
        }

        private static void ClassifyValveHand(
            Component hand,
            ref Component leftHand,
            ref Component rightHand)
        {
            if (hand == null)
            {
                return;
            }

            object handType = ReadMember(hand, "handType");
            string label = handType != null ? handType.ToString() : hand.name;
            if (label.IndexOf("left", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                leftHand = hand;
            }
            else if (label.IndexOf("right", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                rightHand = hand;
            }
        }

        private static object ReadMember(Component component, string name)
        {
            Type type = component.GetType();
            FieldInfo field = type.GetField(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                return field.GetValue(component);
            }

            PropertyInfo property = type.GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return property != null ? property.GetValue(component) : null;
        }

        private static Component FindComponentByFullName(GameObject root, string fullName)
        {
            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component != null && component.GetType().FullName == fullName)
                {
                    return component;
                }
            }

            return null;
        }

        private static int CountMissingScripts(GameObject root)
        {
            int missing = 0;
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                    transforms[i].gameObject);
            }

            return missing;
        }

        private static Transform CreateTrackedTarget(
            Transform parent,
            string name,
            XRNode node,
            Vector3 fallbackLocalPosition)
        {
            Transform target = CreateChild(parent, name);
            target.localPosition = fallbackLocalPosition;
            ApexVRTrackedNode trackedNode = Undo.AddComponent<ApexVRTrackedNode>(target.gameObject);
            trackedNode.EditorConfigure(node);
            return target;
        }

        private static ApexVRPhysicalHand CreatePhysicalHand(
            Transform parent,
            string name,
            Transform trackingTarget,
            Collider playerCollider,
            bool createDebugVisual)
        {
            Transform handTransform = CreateChild(parent, name);
            handTransform.position = trackingTarget.position;
            handTransform.rotation = trackingTarget.rotation;

            SphereCollider handCollider = Undo.AddComponent<SphereCollider>(handTransform.gameObject);
            handCollider.radius = 0.09f;

            Rigidbody handBody = Undo.AddComponent<Rigidbody>(handTransform.gameObject);
            handBody.mass = 1f;
            handBody.useGravity = false;
            handBody.isKinematic = true;
            handBody.interpolation = RigidbodyInterpolation.Interpolate;
            handBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            ApexGrabber grabber = Undo.AddComponent<ApexGrabber>(handTransform.gameObject);
            ApexVRPhysicalHand hand = Undo.AddComponent<ApexVRPhysicalHand>(handTransform.gameObject);
            hand.EditorConfigure(trackingTarget, grabber, handCollider, new[] { playerCollider });

            if (createDebugVisual)
            {
                GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Undo.RegisterCreatedObjectUndo(visual, "Create Apex VR Hand Visual");
                visual.name = "Hand Visual";
                visual.transform.SetParent(handTransform, false);
                visual.transform.localScale = Vector3.one * 0.16f;
                Collider generatedCollider = visual.GetComponent<Collider>();
                if (generatedCollider != null)
                {
                    Undo.DestroyObjectImmediate(generatedCollider);
                }
            }

            return hand;
        }

        private static Transform CreateChild(Transform parent, string name)
        {
            GameObject child = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(child, "Create Apex VR Rig Child");
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static Vector3 FindSpawnPosition()
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null && sceneView.camera != null)
            {
                Vector3 position = sceneView.pivot;
                position.y = Mathf.Max(position.y, 0.1f);
                return position;
            }

            return Vector3.zero;
        }
    }
}
