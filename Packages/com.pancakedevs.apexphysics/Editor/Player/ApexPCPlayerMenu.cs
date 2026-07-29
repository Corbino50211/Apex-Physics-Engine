using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PancakeDevs.ApexPhysics.Editor
{
    internal static class ApexPCPlayerMenu
    {
        [MenuItem("Apex Physics Engine/Player/Create PC Player Rig", priority = 100)]
        private static void CreatePlayerRig()
        {
            GameObject root = new GameObject("Apex PC Player");
            Undo.RegisterCreatedObjectUndo(root, "Create Apex PC Player Rig");
            root.transform.position = FindSpawnPosition();

            CapsuleCollider capsule = Undo.AddComponent<CapsuleCollider>(root);
            capsule.height = 1.8f;
            capsule.radius = 0.35f;
            capsule.center = new Vector3(0f, 0.9f, 0f);

            Rigidbody body = Undo.AddComponent<Rigidbody>(root);
            body.mass = 80f;
            body.useGravity = true;
            body.isKinematic = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            body.constraints = RigidbodyConstraints.FreezeRotation;

            ApexPCPlayerMotor motor = Undo.AddComponent<ApexPCPlayerMotor>(root);
            ApexPCPlayerLook look = Undo.AddComponent<ApexPCPlayerLook>(root);
            ApexPCPlayerInteraction playerInteraction = Undo.AddComponent<ApexPCPlayerInteraction>(root);
            ApexPCPlayerInput input = Undo.AddComponent<ApexPCPlayerInput>(root);

            GameObject cameraPivotObject = new GameObject("Camera Pivot");
            Undo.RegisterCreatedObjectUndo(cameraPivotObject, "Create Apex Camera Pivot");
            Transform cameraPivot = cameraPivotObject.transform;
            cameraPivot.SetParent(root.transform, false);
            cameraPivot.localPosition = new Vector3(0f, 1.65f, 0f);

            GameObject cameraObject = new GameObject("Player Camera");
            Undo.RegisterCreatedObjectUndo(cameraObject, "Create Apex Player Camera");
            Transform cameraTransform = cameraObject.transform;
            cameraTransform.SetParent(cameraPivot, false);
            Camera camera = Undo.AddComponent<Camera>(cameraObject);
            camera.tag = "MainCamera";
            Undo.AddComponent<AudioListener>(cameraObject);

            GameObject interactionObject = new GameObject("Interaction Origin");
            Undo.RegisterCreatedObjectUndo(interactionObject, "Create Apex Interaction Origin");
            Transform interaction = interactionObject.transform;
            interaction.SetParent(cameraTransform, false);
            interaction.localPosition = new Vector3(0f, -0.12f, 1.25f);
            ApexGrabber grabber = Undo.AddComponent<ApexGrabber>(interactionObject);

            GameObject groundProbeObject = new GameObject("Ground Probe");
            Undo.RegisterCreatedObjectUndo(groundProbeObject, "Create Apex Ground Probe");
            Transform groundProbe = groundProbeObject.transform;
            groundProbe.SetParent(root.transform, false);
            groundProbe.localPosition = new Vector3(0f, 0.35f, 0f);

            ConfigureMotor(motor, cameraTransform, groundProbe);
            ConfigureLook(look, root.transform, cameraPivot);
            ConfigureInteraction(playerInteraction, camera, grabber, interaction, new Collider[] { capsule });
            ConfigureInput(input, motor, look, grabber, playerInteraction);

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log(
                "Created Apex PC Player Rig. Controls: WASD, mouse, Space, Shift, E grab/release, mouse wheel hold distance, left click throw, Escape unlock cursor.",
                root);
        }

        [MenuItem("Apex Physics Engine/Player/Upgrade Selected PC Player Interaction", priority = 101)]
        private static void UpgradeSelectedPlayer()
        {
            GameObject root = Selection.activeGameObject;
            if (root == null)
            {
                Debug.LogWarning("Select the root of an existing Apex PC Player rig first.");
                return;
            }

            ApexPCPlayerMotor motor = root.GetComponent<ApexPCPlayerMotor>();
            ApexPCPlayerLook look = root.GetComponent<ApexPCPlayerLook>();
            ApexPCPlayerInput input = root.GetComponent<ApexPCPlayerInput>();
            Camera camera = root.GetComponentInChildren<Camera>(true);
            ApexGrabber grabber = root.GetComponentInChildren<ApexGrabber>(true);

            if (motor == null || look == null || input == null || camera == null || grabber == null)
            {
                Debug.LogError("The selected object is not a complete Apex PC Player rig.", root);
                return;
            }

            ApexPCPlayerInteraction playerInteraction = root.GetComponent<ApexPCPlayerInteraction>();
            if (playerInteraction == null)
            {
                playerInteraction = Undo.AddComponent<ApexPCPlayerInteraction>(root);
            }

            Transform holdTarget = grabber.GripTarget;
            Collider[] playerColliders = root.GetComponentsInChildren<Collider>(true);
            ConfigureInteraction(playerInteraction, camera, grabber, holdTarget, playerColliders);
            ConfigureInput(input, motor, look, grabber, playerInteraction);

            EditorUtility.SetDirty(root);
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log("Upgraded the selected Apex PC Player rig with 0.6.1 interaction controls.", root);
        }

        [MenuItem("Apex Physics Engine/Player/Upgrade Selected PC Player Interaction", true)]
        private static bool ValidateUpgradeSelectedPlayer()
        {
            return Selection.activeGameObject != null;
        }

        private static void ConfigureMotor(ApexPCPlayerMotor motor, Transform movementReference, Transform groundProbe)
        {
            SerializedObject serialized = new SerializedObject(motor);
            serialized.FindProperty("movementReference").objectReferenceValue = movementReference;
            serialized.FindProperty("groundProbe").objectReferenceValue = groundProbe;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureLook(ApexPCPlayerLook look, Transform yawRoot, Transform pitchRoot)
        {
            SerializedObject serialized = new SerializedObject(look);
            serialized.FindProperty("yawRoot").objectReferenceValue = yawRoot;
            serialized.FindProperty("pitchRoot").objectReferenceValue = pitchRoot;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureInteraction(
            ApexPCPlayerInteraction interaction,
            Camera camera,
            ApexGrabber grabber,
            Transform holdTarget,
            Collider[] playerColliders)
        {
            SerializedObject serialized = new SerializedObject(interaction);
            serialized.FindProperty("viewCamera").objectReferenceValue = camera;
            serialized.FindProperty("grabber").objectReferenceValue = grabber;
            serialized.FindProperty("holdTarget").objectReferenceValue = holdTarget;

            SerializedProperty colliders = serialized.FindProperty("playerColliders");
            colliders.arraySize = playerColliders != null ? playerColliders.Length : 0;
            for (int i = 0; i < colliders.arraySize; i++)
            {
                colliders.GetArrayElementAtIndex(i).objectReferenceValue = playerColliders[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureInput(
            ApexPCPlayerInput input,
            ApexPCPlayerMotor motor,
            ApexPCPlayerLook look,
            ApexGrabber grabber,
            ApexPCPlayerInteraction interaction)
        {
            SerializedObject serialized = new SerializedObject(input);
            serialized.FindProperty("motor").objectReferenceValue = motor;
            serialized.FindProperty("look").objectReferenceValue = look;
            serialized.FindProperty("grabber").objectReferenceValue = grabber;
            serialized.FindProperty("interaction").objectReferenceValue = interaction;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Vector3 FindSpawnPosition()
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null && sceneView.camera != null)
            {
                Vector3 position = sceneView.pivot;
                position.y = Mathf.Max(position.y, 1f);
                return position;
            }

            return Vector3.up;
        }
    }
}
