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

            SerializedObject motorObject = new SerializedObject(motor);
            motorObject.FindProperty("movementReference").objectReferenceValue = cameraTransform;
            motorObject.FindProperty("groundProbe").objectReferenceValue = groundProbe;
            motorObject.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject lookObject = new SerializedObject(look);
            lookObject.FindProperty("yawRoot").objectReferenceValue = root.transform;
            lookObject.FindProperty("pitchRoot").objectReferenceValue = cameraPivot;
            lookObject.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject interactionSettings = new SerializedObject(playerInteraction);
            interactionSettings.FindProperty("viewCamera").objectReferenceValue = camera;
            interactionSettings.FindProperty("grabber").objectReferenceValue = grabber;
            interactionSettings.FindProperty("holdTarget").objectReferenceValue = interaction;
            interactionSettings.FindProperty("playerColliders").arraySize = 1;
            interactionSettings.FindProperty("playerColliders").GetArrayElementAtIndex(0).objectReferenceValue = capsule;
            interactionSettings.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject inputObject = new SerializedObject(input);
            inputObject.FindProperty("motor").objectReferenceValue = motor;
            inputObject.FindProperty("look").objectReferenceValue = look;
            inputObject.FindProperty("grabber").objectReferenceValue = grabber;
            inputObject.FindProperty("interaction").objectReferenceValue = playerInteraction;
            inputObject.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log(
                "Created Apex PC Player Rig. Controls: WASD, mouse, Space, Shift, E grab/release, mouse wheel hold distance, left click throw, Escape unlock cursor.",
                root);
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
