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
            "Apex Physics Engine/Player/Create SteamVR OpenVR Player Rig";

        [MenuItem(OpenXRPath, false, 120)]
        private static void CreateOpenXRRig()
        {
            CreateRig(ApexVRBackend.OpenXR);
        }

        [MenuItem(SteamVRPath, false, 121)]
        private static void CreateSteamVRRig()
        {
            CreateRig(ApexVRBackend.SteamVROpenVR);
        }

        private static void CreateRig(ApexVRBackend backend)
        {
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create Apex VR Player Rig");

            GameObject root = new GameObject(
                backend == ApexVRBackend.OpenXR
                    ? "Apex OpenXR VR Player"
                    : "Apex SteamVR OpenVR Player");
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
                capsule);
            ApexVRPhysicalHand rightHand = CreatePhysicalHand(
                root.transform,
                "Right Physical Hand",
                rightTarget,
                capsule);

            rig.EditorConfigure(backend, head, leftHand, rightHand);
            motor.EditorConfigure(head);
            input.EditorConfigure(motor, leftHand.Grabber, rightHand.Grabber);

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(root.scene);
            Undo.CollapseUndoOperations(undoGroup);

            string loaderName = backend == ApexVRBackend.OpenXR
                ? "OpenXR"
                : "OpenVR Loader with the SteamVR Plugin";
            Debug.Log(
                $"Created Apex VR Player Rig for {loaderName}. " +
                "Enable the matching provider in Project Settings > XR Plug-in Management before Play Mode.",
                root);
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
            Collider playerCollider)
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
