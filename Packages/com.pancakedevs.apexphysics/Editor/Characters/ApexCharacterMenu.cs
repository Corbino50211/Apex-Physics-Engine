using PancakeDevs.ApexPhysics;
using UnityEditor;
using UnityEngine;

namespace PancakeDevs.ApexPhysics.Editor
{
    internal static class ApexCharacterMenu
    {
        private const string Root = "Apex Physics Engine/Characters/";
        private const string ProfilesFolder = "Assets/Apex Physics Engine/Profiles";

        [MenuItem(Root + "Create Physical Player Rig", false, 10)]
        private static void CreatePhysicalPlayerRig()
        {
            ApexPhysicalPlayerProfile profile = GetOrCreateProfile<ApexPhysicalPlayerProfile>(
                "Apex Physical Player Profile.asset");

            GameObject setupRoot = new GameObject("Apex Physical Player Rig");
            Undo.RegisterCreatedObjectUndo(setupRoot, "Create Apex Physical Player Rig");
            setupRoot.transform.position = Selection.activeTransform != null
                ? Selection.activeTransform.position
                : Vector3.zero;

            GameObject root = new GameObject("Apex Physical Player");
            Undo.RegisterCreatedObjectUndo(root, "Create Apex Physical Player");
            root.transform.SetParent(setupRoot.transform, false);

            Rigidbody body = Undo.AddComponent<Rigidbody>(root);
            body.useGravity = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            Undo.AddComponent<CapsuleCollider>(root);
            Undo.AddComponent<ApexBody>(root);
            ApexPhysicalPlayerRig rig = Undo.AddComponent<ApexPhysicalPlayerRig>(root);

            Transform trackingRoot = CreateChild(root.transform, "Tracking Targets", Vector3.zero);
            Transform headTarget = CreateChild(trackingRoot, "Head Target", new Vector3(0f, 1.65f, 0f));
            Transform leftHandTarget = CreateChild(trackingRoot, "Left Hand Target", new Vector3(-0.3f, 1.25f, 0.35f));
            Transform rightHandTarget = CreateChild(trackingRoot, "Right Hand Target", new Vector3(0.3f, 1.25f, 0.35f));

            Transform proxyRoot = CreateChild(setupRoot.transform, "Physical Proxies", Vector3.zero);
            ApexTrackedBodyPart physicalHead = CreateTrackedPart(
                proxyRoot,
                root.transform,
                "Physical Head",
                headTarget,
                profile,
                0.13f,
                2f);
            ApexTrackedBodyPart physicalLeftHand = CreateTrackedPart(
                proxyRoot,
                root.transform,
                "Physical Left Hand",
                leftHandTarget,
                profile,
                0.09f,
                1f);
            ApexTrackedBodyPart physicalRightHand = CreateTrackedPart(
                proxyRoot,
                root.transform,
                "Physical Right Hand",
                rightHandTarget,
                profile,
                0.09f,
                1f);

            rig.SetProfile(profile, false);
            rig.SetTrackingTargets(headTarget, leftHandTarget, rightHandTarget);
            rig.SetTrackedBodies(physicalHead, physicalLeftHand, physicalRightHand);
            rig.ApplyProfile();

            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);
        }

        [MenuItem(Root + "Make Selected Object an Active Ragdoll Controller", false, 20)]
        private static void MakeSelectedActiveRagdoll()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                return;
            }

            ApexActiveRagdoll controller = selected.GetComponent<ApexActiveRagdoll>();
            if (controller == null)
            {
                controller = Undo.AddComponent<ApexActiveRagdoll>(selected);
            }

            ApexRagdollProfile profile = GetOrCreateProfile<ApexRagdollProfile>(
                "Apex Ragdoll Profile.asset");
            controller.SetProfile(profile);
            controller.RefreshBones();
            controller.CaptureCurrentPose();

            Animator animator = selected.GetComponent<Animator>();
            if (animator != null)
            {
                SerializedObject serializedController = new SerializedObject(controller);
                SerializedProperty targetAnimator = serializedController.FindProperty("targetAnimator");
                if (targetAnimator != null)
                {
                    targetAnimator.objectReferenceValue = animator;
                    serializedController.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            EditorUtility.SetDirty(controller);
            Selection.activeGameObject = selected;

            if (controller.Bones.Count == 0)
            {
                Debug.LogWarning(
                    "Apex active ragdoll controller added, but no ApexRagdollBone components were found. " +
                    "Add them to the physical skeleton and assign matching animated targets.",
                    selected);
            }
        }

        [MenuItem(Root + "Make Selected Rigidbody a Ragdoll Bone", false, 21)]
        private static void MakeSelectedRagdollBone()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                return;
            }

            Rigidbody body = selected.GetComponent<Rigidbody>();
            if (body == null)
            {
                body = Undo.AddComponent<Rigidbody>(selected);
            }

            ConfigurableJoint joint = selected.GetComponent<ConfigurableJoint>();
            if (joint == null)
            {
                joint = Undo.AddComponent<ConfigurableJoint>(selected);
            }

            ApexRagdollBone bone = selected.GetComponent<ApexRagdollBone>();
            if (bone == null)
            {
                bone = Undo.AddComponent<ApexRagdollBone>(selected);
            }

            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            joint.rotationDriveMode = RotationDriveMode.Slerp;
            bone.CapturePose();

            ApexActiveRagdoll controller = selected.GetComponentInParent<ApexActiveRagdoll>();
            controller?.RefreshBones();

            EditorUtility.SetDirty(selected);
            Selection.activeGameObject = selected;
        }

        [MenuItem(Root + "Make Selected Object an Active Ragdoll Controller", true)]
        [MenuItem(Root + "Make Selected Rigidbody a Ragdoll Bone", true)]
        private static bool ValidateSelectedCharacterObject()
        {
            return Selection.activeGameObject != null;
        }

        private static Transform CreateChild(Transform parent, string name, Vector3 localPosition)
        {
            GameObject child = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(child, "Create " + name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            return child.transform;
        }

        private static ApexTrackedBodyPart CreateTrackedPart(
            Transform proxyParent,
            Transform owner,
            string name,
            Transform target,
            ApexPhysicalPlayerProfile profile,
            float radius,
            float mass)
        {
            GameObject part = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(part, "Create " + name);
            part.transform.SetParent(proxyParent, false);
            part.transform.position = target.position;
            part.transform.rotation = target.rotation;

            SphereCollider collider = Undo.AddComponent<SphereCollider>(part);
            collider.radius = radius;

            Rigidbody body = Undo.AddComponent<Rigidbody>(part);
            body.mass = mass;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            ApexTrackedBodyPart trackedPart = Undo.AddComponent<ApexTrackedBodyPart>(part);
            trackedPart.Configure(profile, target, owner);
            return trackedPart;
        }

        private static T GetOrCreateProfile<T>(string fileName) where T : ScriptableObject
        {
            EnsureFolder("Assets/Apex Physics Engine");
            EnsureFolder(ProfilesFolder);

            string path = ProfilesFolder + "/" + fileName;
            T profile = AssetDatabase.LoadAssetAtPath<T>(path);
            if (profile != null)
            {
                return profile;
            }

            profile = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.SaveAssets();
            return profile;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            int separator = path.LastIndexOf('/');
            if (separator <= 0)
            {
                return;
            }

            string parent = path.Substring(0, separator);
            string name = path.Substring(separator + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
