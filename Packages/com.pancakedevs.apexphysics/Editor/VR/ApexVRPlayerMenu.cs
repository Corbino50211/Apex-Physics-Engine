using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR;

namespace PancakeDevs.ApexPhysics.Editor
{
    internal static class ApexVRPlayerMenu
    {
        private const string CreatePath =
            "Apex Physics Engine/Player/Create Apex Physical OpenXR Rig";
        private const string BindHumanoidPath =
            "Apex Physics Engine/Player/Bind Selected Humanoid to Physical OpenXR Rig";

        [MenuItem(CreatePath, false, 120)]
        private static void CreatePhysicalOpenXRRig()
        {
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create Apex Physical OpenXR Rig");

            GameObject root = new GameObject("Apex Physical OpenXR Player");
            Undo.RegisterCreatedObjectUndo(root, "Create Apex Physical OpenXR Rig");
            root.transform.position = FindSpawnPosition();

            CapsuleCollider capsule = Undo.AddComponent<CapsuleCollider>(root);
            capsule.radius = 0.3f;
            capsule.height = 1.75f;
            capsule.center = new Vector3(0f, 0.875f, 0f);

            Rigidbody body = Undo.AddComponent<Rigidbody>(root);
            body.mass = 80f;
            body.useGravity = true;
            body.isKinematic = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.constraints = RigidbodyConstraints.FreezeRotation;

            ApexPhysicalOpenXRBody physicalBody =
                Undo.AddComponent<ApexPhysicalOpenXRBody>(root);
            ApexPhysicalOpenXRInput input =
                Undo.AddComponent<ApexPhysicalOpenXRInput>(root);

            Transform trackingOrigin = CreateChild(root.transform, "OpenXR Tracking Origin");
            Transform head = CreateTrackedTarget(
                trackingOrigin,
                "Tracked Head",
                XRNode.CenterEye,
                new Vector3(0f, 1.7f, 0f));

            Camera camera = Undo.AddComponent<Camera>(head.gameObject);
            camera.tag = "MainCamera";
            camera.nearClipPlane = 0.05f;
            Undo.AddComponent<AudioListener>(head.gameObject);

            Transform leftTarget = CreateTrackedTarget(
                trackingOrigin,
                "Left Controller Target",
                XRNode.LeftHand,
                new Vector3(-0.25f, 1.3f, 0.35f));
            Transform rightTarget = CreateTrackedTarget(
                trackingOrigin,
                "Right Controller Target",
                XRNode.RightHand,
                new Vector3(0.25f, 1.3f, 0.35f));

            Transform leftShoulder = CreateChild(head, "Left Shoulder Anchor");
            leftShoulder.localPosition = new Vector3(-0.18f, -0.2f, 0f);
            Transform rightShoulder = CreateChild(head, "Right Shoulder Anchor");
            rightShoulder.localPosition = new Vector3(0.18f, -0.2f, 0f);

            ApexPhysicalOpenXRHand leftHand = CreatePhysicalHand(
                root.transform,
                "Left Physical Hand",
                leftTarget,
                leftShoulder,
                capsule);
            ApexPhysicalOpenXRHand rightHand = CreatePhysicalHand(
                root.transform,
                "Right Physical Hand",
                rightTarget,
                rightShoulder,
                capsule);

            Collider leftCollider = leftHand.GetComponent<Collider>();
            Collider rightCollider = rightHand.GetComponent<Collider>();
            Physics.IgnoreCollision(leftCollider, capsule, true);
            Physics.IgnoreCollision(rightCollider, capsule, true);
            Physics.IgnoreCollision(leftCollider, rightCollider, true);

            physicalBody.EditorConfigure(trackingOrigin, head);
            input.EditorConfigure(physicalBody, leftHand.Grabber, rightHand.Grabber);

            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);
            EditorSceneManager.MarkSceneDirty(root.scene);
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log(
                "Created Apex Physical OpenXR Rig. Enable OpenXR for the Standalone target before entering Play Mode.",
                root);
        }

        [MenuItem(BindHumanoidPath, false, 121)]
        private static void BindSelectedHumanoid()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog(
                    "No Humanoid Selected",
                    "Drag the Humanoid into the scene and select its root in the Hierarchy.",
                    "OK");
                return;
            }

            Animator animator = selected.GetComponentInChildren<Animator>(true);
            if (animator == null || !animator.isHuman || animator.avatar == null || !animator.avatar.isValid)
            {
                EditorUtility.DisplayDialog(
                    "Valid Humanoid Required",
                    "Set the FBX Rig type to Humanoid, press Apply, then select its scene instance.",
                    "OK");
                return;
            }

            ApexPhysicalOpenXRBody physicalBody = Object.FindFirstObjectByType<ApexPhysicalOpenXRBody>(
                FindObjectsInactive.Include);
            if (physicalBody == null)
            {
                EditorUtility.DisplayDialog(
                    "OpenXR Rig Not Found",
                    "Create an Apex Physical OpenXR Rig first.",
                    "OK");
                return;
            }

            FindRigHands(physicalBody, out ApexPhysicalOpenXRHand leftHand, out ApexPhysicalOpenXRHand rightHand);
            if (leftHand == null || rightHand == null || physicalBody.Head == null)
            {
                EditorUtility.DisplayDialog(
                    "Rig References Missing",
                    "Apex could not find the tracked head and both physical hands under the rig.",
                    "OK");
                return;
            }

            Type avatarType = Type.GetType(
                "PancakeDevs.ApexPhysics.ApexPhysicalVRAvatar, PancakeDevs.ApexPhysics.Runtime");
            if (avatarType == null || !typeof(MonoBehaviour).IsAssignableFrom(avatarType))
            {
                EditorUtility.DisplayDialog(
                    "Avatar Runtime Missing",
                    "ApexPhysicalVRAvatar was not imported into the runtime assembly.",
                    "OK");
                return;
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Bind Humanoid to Apex Physical VR Rig");

            Undo.SetTransformParent(selected.transform, physicalBody.transform, "Parent Apex VR Avatar");
            selected.transform.localPosition = Vector3.zero;
            selected.transform.localRotation = Quaternion.identity;

            Component avatar = selected.GetComponent(avatarType);
            if (avatar == null)
            {
                avatar = Undo.AddComponent(selected, avatarType);
            }

            SerializedObject serializedAvatar = new SerializedObject(avatar);
            SetObjectReference(serializedAvatar, "animator", animator);
            SetObjectReference(serializedAvatar, "physicalBody", physicalBody);
            SetObjectReference(serializedAvatar, "headTarget", physicalBody.Head);
            SetObjectReference(serializedAvatar, "leftHandTarget", leftHand.transform);
            SetObjectReference(serializedAvatar, "rightHandTarget", rightHand.transform);
            serializedAvatar.ApplyModifiedProperties();

            HideDebugHandVisuals(leftHand);
            HideDebugHandVisuals(rightHand);

            EditorUtility.SetDirty(selected);
            EditorUtility.SetDirty(avatar);
            EditorSceneManager.MarkSceneDirty(selected.scene);
            Selection.activeGameObject = selected;
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("Bound the selected Humanoid to the Apex Physical OpenXR rig.", selected);
        }

        [MenuItem(BindHumanoidPath, true)]
        private static bool ValidateBindSelectedHumanoid()
        {
            return Selection.activeGameObject != null;
        }

        private static void FindRigHands(
            ApexPhysicalOpenXRBody physicalBody,
            out ApexPhysicalOpenXRHand leftHand,
            out ApexPhysicalOpenXRHand rightHand)
        {
            leftHand = null;
            rightHand = null;

            ApexPhysicalOpenXRHand[] hands = Object.FindObjectsByType<ApexPhysicalOpenXRHand>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (ApexPhysicalOpenXRHand hand in hands)
            {
                if (hand == null || !hand.transform.IsChildOf(physicalBody.transform))
                {
                    continue;
                }

                string lowerName = hand.name.ToLowerInvariant();
                if (lowerName.Contains("left"))
                {
                    leftHand = hand;
                }
                else if (lowerName.Contains("right"))
                {
                    rightHand = hand;
                }
            }
        }

        private static void SetObjectReference(
            SerializedObject serializedObject,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.objectReferenceValue = value;
            }
        }

        private static void HideDebugHandVisuals(ApexPhysicalOpenXRHand hand)
        {
            Renderer[] renderers = hand.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                string lowerName = renderer.name.ToLowerInvariant();
                if (lowerName.Contains("visual") || lowerName.Contains("sphere") || lowerName.Contains("debug"))
                {
                    Undo.RecordObject(renderer, "Hide Apex Debug Hand Visual");
                    renderer.enabled = false;
                    EditorUtility.SetDirty(renderer);
                }
            }
        }

        private static Transform CreateTrackedTarget(
            Transform parent,
            string name,
            XRNode node,
            Vector3 fallbackLocalPosition)
        {
            Transform target = CreateChild(parent, name);
            target.localPosition = fallbackLocalPosition;
            ApexVRTrackedNode tracker = Undo.AddComponent<ApexVRTrackedNode>(target.gameObject);
            tracker.EditorConfigure(node);
            return target;
        }

        private static ApexPhysicalOpenXRHand CreatePhysicalHand(
            Transform parent,
            string name,
            Transform trackingTarget,
            Transform shoulder,
            Collider playerCollider)
        {
            Transform handTransform = CreateChild(parent, name);
            handTransform.position = trackingTarget.position;
            handTransform.rotation = trackingTarget.rotation;

            SphereCollider collider = Undo.AddComponent<SphereCollider>(handTransform.gameObject);
            collider.radius = 0.085f;

            Rigidbody rigidbody = Undo.AddComponent<Rigidbody>(handTransform.gameObject);
            rigidbody.mass = 1.2f;
            rigidbody.useGravity = false;
            rigidbody.isKinematic = false;
            rigidbody.interpolation = RigidbodyInterpolation.None;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rigidbody.linearDamping = 0.2f;
            rigidbody.angularDamping = 0.2f;

            ApexGrabber grabber = Undo.AddComponent<ApexGrabber>(handTransform.gameObject);
            ApexPhysicalOpenXRHand hand =
                Undo.AddComponent<ApexPhysicalOpenXRHand>(handTransform.gameObject);
            hand.EditorConfigure(trackingTarget, shoulder, grabber);

            Physics.IgnoreCollision(collider, playerCollider, true);

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Undo.RegisterCreatedObjectUndo(visual, "Create Apex Physical Hand Visual");
            visual.name = "Hand Visual";
            visual.transform.SetParent(handTransform, false);
            visual.transform.localScale = Vector3.one * 0.16f;
            Collider generatedCollider = visual.GetComponent<Collider>();
            if (generatedCollider != null)
            {
                Undo.DestroyObjectImmediate(generatedCollider);
            }

            return hand;
        }

        private static Transform CreateChild(Transform parent, string name)
        {
            GameObject child = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(child, "Create Apex Physical OpenXR Child");
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static Vector3 FindSpawnPosition()
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view != null && view.camera != null)
            {
                Vector3 position = view.pivot;
                position.y = Mathf.Max(0.1f, position.y);
                return position;
            }

            return Vector3.zero;
        }
    }
}
