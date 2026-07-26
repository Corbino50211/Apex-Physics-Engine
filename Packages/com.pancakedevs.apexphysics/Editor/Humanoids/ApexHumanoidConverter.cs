using System;
using System.Collections.Generic;
using System.IO;
using PancakeDevs.ApexPhysics;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace PancakeDevs.ApexPhysics.Editor
{
    internal static class ApexHumanoidConverter
    {
        private const string ProfilesFolder = "Assets/Apex Physics Engine/Profiles";
        private const string HumanoidsFolder = "Assets/Apex Physics Engine/Humanoids";
        private const string CratesFolder = "Assets/Apex Physics Engine/Warehouse/Crates";

        private static readonly HumanBodyBones[] SupportedBones =
        {
            HumanBodyBones.Hips,
            HumanBodyBones.Spine,
            HumanBodyBones.Chest,
            HumanBodyBones.UpperChest,
            HumanBodyBones.Neck,
            HumanBodyBones.Head,
            HumanBodyBones.LeftShoulder,
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.LeftLowerArm,
            HumanBodyBones.LeftHand,
            HumanBodyBones.RightShoulder,
            HumanBodyBones.RightUpperArm,
            HumanBodyBones.RightLowerArm,
            HumanBodyBones.RightHand,
            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.LeftFoot,
            HumanBodyBones.LeftToes,
            HumanBodyBones.RightUpperLeg,
            HumanBodyBones.RightLowerLeg,
            HumanBodyBones.RightFoot,
            HumanBodyBones.RightToes
        };

        private static readonly HumanBodyBones[] RequiredBones =
        {
            HumanBodyBones.Hips,
            HumanBodyBones.Head,
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.LeftLowerArm,
            HumanBodyBones.LeftHand,
            HumanBodyBones.RightUpperArm,
            HumanBodyBones.RightLowerArm,
            HumanBodyBones.RightHand,
            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.LeftFoot,
            HumanBodyBones.RightUpperLeg,
            HumanBodyBones.RightLowerLeg,
            HumanBodyBones.RightFoot
        };

        public static bool CanConvert(GameObject candidate, out string reason)
        {
            reason = string.Empty;
            if (candidate == null)
            {
                reason = "Select an imported humanoid character or a scene instance.";
                return false;
            }

            if (!EditorUtility.IsPersistent(candidate) &&
                candidate.GetComponentInParent<ApexPhysicalHumanoid>() != null)
            {
                reason = "This character is already inside an Apex physical humanoid rig.";
                return false;
            }

            Animator animator = candidate.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                reason = "The selected object does not contain an Animator.";
                return false;
            }

            if (animator.avatar == null || !animator.avatar.isValid || !animator.isHuman)
            {
                reason = "The Animator must use a valid Humanoid Avatar in the model import settings.";
                return false;
            }

            for (int i = 0; i < RequiredBones.Length; i++)
            {
                if (animator.GetBoneTransform(RequiredBones[i]) == null)
                {
                    reason = $"The Humanoid Avatar is missing required bone {RequiredBones[i]}.";
                    return false;
                }
            }

            return true;
        }

        public static ApexPhysicalHumanoid Convert(
            GameObject selected,
            ApexPhysicalHumanoidMode mode,
            bool saveAsPrefab,
            bool createCrate)
        {
            GameObject source = PrepareSceneInstance(selected);
            if (!CanConvert(source, out string reason))
            {
                EditorUtility.DisplayDialog("Apex Humanoid Converter", reason, "OK");
                return null;
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Convert Apex Physical Humanoid");

            try
            {
                Animator physicalAnimator = source.GetComponentInChildren<Animator>(true);
                Transform originalParent = source.transform.parent;
                int originalSiblingIndex = source.transform.GetSiblingIndex();

                GameObject targetCharacter = UnityEngine.Object.Instantiate(source, originalParent);
                targetCharacter.name = source.name + " Animated Target";
                targetCharacter.transform.SetPositionAndRotation(
                    source.transform.position,
                    source.transform.rotation);
                targetCharacter.transform.localScale = source.transform.localScale;
                Undo.RegisterCreatedObjectUndo(targetCharacter, "Create Animated Humanoid Target");

                Animator targetAnimator = targetCharacter.GetComponentInChildren<Animator>(true);
                if (targetAnimator == null || !targetAnimator.isHuman)
                {
                    Undo.DestroyObjectImmediate(targetCharacter);
                    EditorUtility.DisplayDialog(
                        "Apex Humanoid Converter",
                        "Apex could not duplicate the Humanoid Animator.",
                        "OK");
                    return null;
                }

                GameObject wrapper = new GameObject(source.name + " Apex Physical Humanoid");
                Undo.RegisterCreatedObjectUndo(wrapper, "Create Apex Physical Humanoid Root");
                wrapper.transform.SetParent(originalParent, false);
                wrapper.transform.SetSiblingIndex(originalSiblingIndex);
                wrapper.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                wrapper.transform.localScale = Vector3.one;

                Undo.RecordObject(source.transform, "Parent Physical Character");
                source.transform.SetParent(wrapper.transform, true);
                Undo.RecordObject(targetCharacter.transform, "Parent Animated Target");
                targetCharacter.transform.SetParent(wrapper.transform, true);

                string originalName = source.name;
                source.name = originalName + " Physical";

                Dictionary<HumanBodyBones, Transform> physicalBones = BuildBoneMap(physicalAnimator);
                Dictionary<HumanBodyBones, Transform> targetBones = BuildBoneMap(targetAnimator);

                PreparePhysicalCharacter(source, physicalAnimator);
                PrepareAnimatedTarget(targetCharacter, targetAnimator);

                float characterHeight = CalculateCharacterHeight(physicalBones, wrapper.transform.up);
                Dictionary<Transform, Rigidbody> bodiesByTransform = new Dictionary<Transform, Rigidbody>();
                Dictionary<HumanBodyBones, ApexRagdollBone> apexBones =
                    new Dictionary<HumanBodyBones, ApexRagdollBone>();

                for (int i = 0; i < SupportedBones.Length; i++)
                {
                    HumanBodyBones role = SupportedBones[i];
                    if (!physicalBones.TryGetValue(role, out Transform physicalBone) ||
                        !targetBones.TryGetValue(role, out Transform targetBone))
                    {
                        continue;
                    }

                    Rigidbody body = GetOrAdd<Rigidbody>(physicalBone.gameObject);
                    ConfigureBody(body, role);
                    bodiesByTransform[physicalBone] = body;

                    Transform childBone = GetPrimaryChild(role, physicalBones);
                    EnsureCollider(physicalBone, childBone, role, characterHeight);

                    ConfigurableJoint joint = GetOrAdd<ConfigurableJoint>(physicalBone.gameObject);
                    Rigidbody connectedBody = role == HumanBodyBones.Hips
                        ? null
                        : FindNearestParentBody(physicalBone.parent, bodiesByTransform);
                    ConfigureJoint(joint, role, connectedBody);

                    ApexRagdollBone apexBone = GetOrAdd<ApexRagdollBone>(physicalBone.gameObject);
                    bool isRoot = role == HumanBodyBones.Hips;
                    apexBone.Configure(targetBone, isRoot);
                    apexBone.SetMuscleMultiplier(isRoot ? 0f : GetMuscleMultiplier(role));
                    apexBones[role] = apexBone;
                }

                if (!apexBones.TryGetValue(HumanBodyBones.Hips, out ApexRagdollBone physicalHips))
                {
                    throw new InvalidOperationException("Apex failed to configure the physical hips bone.");
                }

                Transform targetHips = targetBones[HumanBodyBones.Hips];
                ApexRagdollProfile ragdollProfile = GetOrCreateProfile<ApexRagdollProfile>(
                    "Apex Humanoid Ragdoll Profile.asset");

                ApexActiveRagdoll activeRagdoll = GetOrAdd<ApexActiveRagdoll>(source);
                activeRagdoll.Configure(
                    ragdollProfile,
                    targetAnimator,
                    physicalHips,
                    false,
                    false);

                ApexHumanoidTargetRootDriver targetRootDriver =
                    GetOrAdd<ApexHumanoidTargetRootDriver>(wrapper);
                targetRootDriver.Configure(physicalHips.transform, targetHips, true, true);

                ApexRagdollCollisionFilter collisionFilter =
                    GetOrAdd<ApexRagdollCollisionFilter>(source);
                collisionFilter.Configure(
                    source.GetComponentsInChildren<Collider>(true),
                    source.GetComponentsInChildren<ConfigurableJoint>(true),
                    false);

                ApexBody hipsApexBody = GetOrAdd<ApexBody>(physicalHips.gameObject);
                _ = hipsApexBody;

                ApexHumanoidTrackingDriver trackingDriver = null;
                ApexHumanoidPlayerMotor playerMotor = null;
                ApexNPCNavigator npcNavigator = null;
                ApexNPCMotor npcMotor = null;
                ApexNPCBrain npcBrain = null;

                if (mode == ApexPhysicalHumanoidMode.PhysicalPlayer)
                {
                    ConfigurePhysicalPlayer(
                        wrapper,
                        targetAnimator,
                        physicalHips,
                        activeRagdoll,
                        targetBones,
                        out trackingDriver,
                        out playerMotor);
                }
                else if (mode == ApexPhysicalHumanoidMode.PhysicalNPC)
                {
                    ConfigurePhysicalNpc(
                        physicalHips,
                        characterHeight,
                        out npcNavigator,
                        out npcMotor,
                        out npcBrain);
                }

                ApexPhysicalHumanoid humanoid = GetOrAdd<ApexPhysicalHumanoid>(wrapper);
                humanoid.Configure(
                    mode,
                    targetCharacter,
                    source,
                    targetAnimator,
                    targetHips,
                    physicalHips,
                    activeRagdoll,
                    targetRootDriver,
                    trackingDriver,
                    collisionFilter,
                    playerMotor,
                    npcNavigator,
                    npcMotor,
                    npcBrain);

                activeRagdoll.RefreshBones();
                activeRagdoll.CaptureCurrentPose();
                targetRootDriver.SnapNow();

                EditorUtility.SetDirty(wrapper);
                EditorUtility.SetDirty(source);
                EditorUtility.SetDirty(targetCharacter);

                GameObject savedPrefab = null;
                if (saveAsPrefab)
                {
                    savedPrefab = SaveHumanoidPrefab(wrapper, originalName, mode);
                }

                if (createCrate)
                {
                    if (savedPrefab == null)
                    {
                        savedPrefab = SaveHumanoidPrefab(wrapper, originalName, mode);
                    }

                    CreateSpawnableCrate(savedPrefab, originalName, mode);
                }

                Selection.activeGameObject = wrapper;
                EditorGUIUtility.PingObject(wrapper);
                Undo.CollapseUndoOperations(undoGroup);
                return humanoid;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Undo.RevertAllDownToGroup(undoGroup);
                EditorUtility.DisplayDialog(
                    "Apex Humanoid Converter",
                    "The conversion stopped because of an error. Check the Console for the exact details.",
                    "OK");
                return null;
            }
        }

        private static GameObject PrepareSceneInstance(GameObject selected)
        {
            if (selected == null || !EditorUtility.IsPersistent(selected))
            {
                return selected;
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(selected) as GameObject;
            if (instance == null)
            {
                instance = UnityEngine.Object.Instantiate(selected);
            }

            Undo.RegisterCreatedObjectUndo(instance, "Instantiate Humanoid Character");
            Vector3 position = SceneView.lastActiveSceneView != null
                ? SceneView.lastActiveSceneView.pivot
                : Vector3.zero;
            instance.transform.position = position;
            Selection.activeGameObject = instance;
            return instance;
        }

        private static Dictionary<HumanBodyBones, Transform> BuildBoneMap(Animator animator)
        {
            Dictionary<HumanBodyBones, Transform> map =
                new Dictionary<HumanBodyBones, Transform>();
            for (int i = 0; i < SupportedBones.Length; i++)
            {
                Transform bone = animator.GetBoneTransform(SupportedBones[i]);
                if (bone != null)
                {
                    map[SupportedBones[i]] = bone;
                }
            }

            return map;
        }

        private static void PreparePhysicalCharacter(GameObject character, Animator animator)
        {
            Animator[] animators = character.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                Undo.RecordObject(animators[i], "Disable Physical Animator");
                animators[i].applyRootMotion = false;
                animators[i].enabled = false;
            }

            if (animator != null)
            {
                animator.enabled = false;
            }
        }

        private static void PrepareAnimatedTarget(GameObject character, Animator animator)
        {
            Renderer[] renderers = character.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Undo.RecordObject(renderers[i], "Hide Animated Target Renderer");
                renderers[i].enabled = false;
            }

            Joint[] joints = character.GetComponentsInChildren<Joint>(true);
            for (int i = joints.Length - 1; i >= 0; i--)
            {
                Undo.DestroyObjectImmediate(joints[i]);
            }

            Rigidbody[] bodies = character.GetComponentsInChildren<Rigidbody>(true);
            for (int i = bodies.Length - 1; i >= 0; i--)
            {
                Undo.DestroyObjectImmediate(bodies[i]);
            }

            Collider[] colliders = character.GetComponentsInChildren<Collider>(true);
            for (int i = colliders.Length - 1; i >= 0; i--)
            {
                Undo.DestroyObjectImmediate(colliders[i]);
            }

            MonoBehaviour[] behaviours = character.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] == null)
                {
                    continue;
                }

                Undo.RecordObject(behaviours[i], "Disable Animated Target Behaviour");
                behaviours[i].enabled = false;
            }

            animator.enabled = true;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        private static void ConfigurePhysicalPlayer(
            GameObject wrapper,
            Animator targetAnimator,
            ApexRagdollBone physicalHips,
            ApexActiveRagdoll activeRagdoll,
            Dictionary<HumanBodyBones, Transform> targetBones,
            out ApexHumanoidTrackingDriver trackingDriver,
            out ApexHumanoidPlayerMotor playerMotor)
        {
            Transform trackingRoot = CreateChild(wrapper.transform, "VR Tracking Targets");
            Transform headTarget = CreateTrackingTarget(
                trackingRoot,
                "Head Target",
                targetBones[HumanBodyBones.Head]);
            Transform leftHandTarget = CreateTrackingTarget(
                trackingRoot,
                "Left Hand Target",
                targetBones[HumanBodyBones.LeftHand]);
            Transform rightHandTarget = CreateTrackingTarget(
                trackingRoot,
                "Right Hand Target",
                targetBones[HumanBodyBones.RightHand]);

            trackingRoot.SetParent(physicalHips.transform, true);

            trackingDriver = GetOrAdd<ApexHumanoidTrackingDriver>(wrapper);
            trackingDriver.Configure(targetAnimator, headTarget, leftHandTarget, rightHandTarget);

            ApexPhysicalPlayerProfile playerProfile =
                GetOrCreateProfile<ApexPhysicalPlayerProfile>(
                    "Apex Humanoid Player Profile.asset");
            playerMotor = GetOrAdd<ApexHumanoidPlayerMotor>(physicalHips.gameObject);
            playerMotor.Configure(playerProfile, activeRagdoll, headTarget);
        }

        private static void ConfigurePhysicalNpc(
            ApexRagdollBone physicalHips,
            float characterHeight,
            out ApexNPCNavigator navigator,
            out ApexNPCMotor motor,
            out ApexNPCBrain brain)
        {
            GameObject hips = physicalHips.gameObject;
            NavMeshAgent agent = GetOrAdd<NavMeshAgent>(hips);
            agent.radius = Mathf.Clamp(characterHeight * 0.16f, 0.15f, 0.5f);
            agent.height = Mathf.Max(characterHeight, agent.radius * 2f);
            agent.baseOffset = -characterHeight * 0.5f;
            agent.updatePosition = false;
            agent.updateRotation = false;
            agent.updateUpAxis = false;

            navigator = GetOrAdd<ApexNPCNavigator>(hips);
            motor = GetOrAdd<ApexNPCMotor>(hips);
            brain = GetOrAdd<ApexNPCBrain>(hips);
        }

        private static void ConfigureBody(Rigidbody body, HumanBodyBones role)
        {
            Undo.RecordObject(body, "Configure Humanoid Rigidbody");
            body.mass = GetBoneMass(role);
            body.useGravity = true;
            body.isKinematic = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.maxAngularVelocity = 50f;
            body.solverIterations = 12;
            body.solverVelocityIterations = 4;
        }

        private static void ConfigureJoint(
            ConfigurableJoint joint,
            HumanBodyBones role,
            Rigidbody connectedBody)
        {
            Undo.RecordObject(joint, "Configure Humanoid Joint");
            joint.connectedBody = connectedBody;
            joint.autoConfigureConnectedAnchor = true;
            joint.axis = Vector3.right;
            joint.secondaryAxis = Vector3.up;
            joint.configuredInWorldSpace = false;
            joint.rotationDriveMode = RotationDriveMode.Slerp;
            joint.enableCollision = false;
            joint.enablePreprocessing = false;
            joint.projectionMode = JointProjectionMode.PositionAndRotation;
            joint.projectionDistance = 0.05f;
            joint.projectionAngle = 10f;

            if (connectedBody == null)
            {
                joint.xMotion = ConfigurableJointMotion.Free;
                joint.yMotion = ConfigurableJointMotion.Free;
                joint.zMotion = ConfigurableJointMotion.Free;
                joint.angularXMotion = ConfigurableJointMotion.Free;
                joint.angularYMotion = ConfigurableJointMotion.Free;
                joint.angularZMotion = ConfigurableJointMotion.Free;
                return;
            }

            joint.xMotion = ConfigurableJointMotion.Locked;
            joint.yMotion = ConfigurableJointMotion.Locked;
            joint.zMotion = ConfigurableJointMotion.Locked;
            joint.angularXMotion = ConfigurableJointMotion.Limited;
            joint.angularYMotion = ConfigurableJointMotion.Limited;
            joint.angularZMotion = ConfigurableJointMotion.Limited;

            GetJointLimits(role, out float lowX, out float highX, out float y, out float z);
            joint.lowAngularXLimit = CreateLimit(lowX);
            joint.highAngularXLimit = CreateLimit(highX);
            joint.angularYLimit = CreateLimit(y);
            joint.angularZLimit = CreateLimit(z);
        }

        private static SoftJointLimit CreateLimit(float value)
        {
            return new SoftJointLimit
            {
                limit = value,
                bounciness = 0f,
                contactDistance = 2f
            };
        }

        private static void GetJointLimits(
            HumanBodyBones role,
            out float lowX,
            out float highX,
            out float y,
            out float z)
        {
            if (role == HumanBodyBones.LeftLowerArm ||
                role == HumanBodyBones.RightLowerArm ||
                role == HumanBodyBones.LeftLowerLeg ||
                role == HumanBodyBones.RightLowerLeg)
            {
                lowX = -10f;
                highX = 120f;
                y = 18f;
                z = 18f;
                return;
            }

            if (role == HumanBodyBones.Spine ||
                role == HumanBodyBones.Chest ||
                role == HumanBodyBones.UpperChest ||
                role == HumanBodyBones.Neck ||
                role == HumanBodyBones.Head)
            {
                lowX = -30f;
                highX = 30f;
                y = 30f;
                z = 25f;
                return;
            }

            lowX = -60f;
            highX = 60f;
            y = 45f;
            z = 45f;
        }

        private static void EnsureCollider(
            Transform bone,
            Transform childBone,
            HumanBodyBones role,
            float characterHeight)
        {
            if (bone.GetComponent<Collider>() != null)
            {
                return;
            }

            if (IsEndBone(role))
            {
                SphereCollider sphere = Undo.AddComponent<SphereCollider>(bone.gameObject);
                sphere.radius = GetRadius(role, characterHeight, characterHeight * 0.12f);
                sphere.center = Vector3.zero;
                return;
            }

            CapsuleCollider capsule = Undo.AddComponent<CapsuleCollider>(bone.gameObject);
            Vector3 localDelta = childBone != null
                ? bone.InverseTransformPoint(childBone.position)
                : Vector3.up * characterHeight * 0.12f;
            float length = Mathf.Max(localDelta.magnitude, characterHeight * 0.04f);
            float radius = GetRadius(role, characterHeight, length);

            capsule.direction = GetDominantAxis(localDelta);
            capsule.center = localDelta * 0.5f;
            capsule.radius = Mathf.Min(radius, length * 0.48f);
            capsule.height = Mathf.Max(length, capsule.radius * 2f);
        }

        private static bool IsEndBone(HumanBodyBones role)
        {
            return role == HumanBodyBones.Head ||
                   role == HumanBodyBones.LeftHand ||
                   role == HumanBodyBones.RightHand ||
                   role == HumanBodyBones.LeftFoot ||
                   role == HumanBodyBones.RightFoot ||
                   role == HumanBodyBones.LeftToes ||
                   role == HumanBodyBones.RightToes;
        }

        private static float GetRadius(
            HumanBodyBones role,
            float characterHeight,
            float boneLength)
        {
            float ratio;
            switch (role)
            {
                case HumanBodyBones.Hips:
                    ratio = 0.11f;
                    break;
                case HumanBodyBones.Spine:
                case HumanBodyBones.Chest:
                case HumanBodyBones.UpperChest:
                    ratio = 0.095f;
                    break;
                case HumanBodyBones.Neck:
                    ratio = 0.045f;
                    break;
                case HumanBodyBones.Head:
                    ratio = 0.075f;
                    break;
                case HumanBodyBones.LeftUpperLeg:
                case HumanBodyBones.RightUpperLeg:
                    ratio = 0.065f;
                    break;
                case HumanBodyBones.LeftLowerLeg:
                case HumanBodyBones.RightLowerLeg:
                    ratio = 0.05f;
                    break;
                case HumanBodyBones.LeftUpperArm:
                case HumanBodyBones.RightUpperArm:
                case HumanBodyBones.LeftShoulder:
                case HumanBodyBones.RightShoulder:
                    ratio = 0.045f;
                    break;
                case HumanBodyBones.LeftLowerArm:
                case HumanBodyBones.RightLowerArm:
                    ratio = 0.035f;
                    break;
                case HumanBodyBones.LeftHand:
                case HumanBodyBones.RightHand:
                    ratio = 0.035f;
                    break;
                default:
                    ratio = 0.045f;
                    break;
            }

            return Mathf.Clamp(characterHeight * ratio, 0.02f, boneLength * 0.48f);
        }

        private static int GetDominantAxis(Vector3 vector)
        {
            Vector3 absolute = new Vector3(
                Mathf.Abs(vector.x),
                Mathf.Abs(vector.y),
                Mathf.Abs(vector.z));
            if (absolute.x >= absolute.y && absolute.x >= absolute.z)
            {
                return 0;
            }

            return absolute.y >= absolute.z ? 1 : 2;
        }

        private static Transform GetPrimaryChild(
            HumanBodyBones role,
            Dictionary<HumanBodyBones, Transform> bones)
        {
            HumanBodyBones childRole;
            switch (role)
            {
                case HumanBodyBones.Hips:
                    childRole = HumanBodyBones.Spine;
                    break;
                case HumanBodyBones.Spine:
                    childRole = bones.ContainsKey(HumanBodyBones.Chest)
                        ? HumanBodyBones.Chest
                        : HumanBodyBones.UpperChest;
                    break;
                case HumanBodyBones.Chest:
                    childRole = bones.ContainsKey(HumanBodyBones.UpperChest)
                        ? HumanBodyBones.UpperChest
                        : HumanBodyBones.Neck;
                    break;
                case HumanBodyBones.UpperChest:
                    childRole = HumanBodyBones.Neck;
                    break;
                case HumanBodyBones.Neck:
                    childRole = HumanBodyBones.Head;
                    break;
                case HumanBodyBones.LeftShoulder:
                    childRole = HumanBodyBones.LeftUpperArm;
                    break;
                case HumanBodyBones.LeftUpperArm:
                    childRole = HumanBodyBones.LeftLowerArm;
                    break;
                case HumanBodyBones.LeftLowerArm:
                    childRole = HumanBodyBones.LeftHand;
                    break;
                case HumanBodyBones.RightShoulder:
                    childRole = HumanBodyBones.RightUpperArm;
                    break;
                case HumanBodyBones.RightUpperArm:
                    childRole = HumanBodyBones.RightLowerArm;
                    break;
                case HumanBodyBones.RightLowerArm:
                    childRole = HumanBodyBones.RightHand;
                    break;
                case HumanBodyBones.LeftUpperLeg:
                    childRole = HumanBodyBones.LeftLowerLeg;
                    break;
                case HumanBodyBones.LeftLowerLeg:
                    childRole = HumanBodyBones.LeftFoot;
                    break;
                case HumanBodyBones.LeftFoot:
                    childRole = HumanBodyBones.LeftToes;
                    break;
                case HumanBodyBones.RightUpperLeg:
                    childRole = HumanBodyBones.RightLowerLeg;
                    break;
                case HumanBodyBones.RightLowerLeg:
                    childRole = HumanBodyBones.RightFoot;
                    break;
                case HumanBodyBones.RightFoot:
                    childRole = HumanBodyBones.RightToes;
                    break;
                default:
                    return null;
            }

            return bones.TryGetValue(childRole, out Transform child) ? child : null;
        }

        private static Rigidbody FindNearestParentBody(
            Transform parent,
            Dictionary<Transform, Rigidbody> bodiesByTransform)
        {
            Transform current = parent;
            while (current != null)
            {
                if (bodiesByTransform.TryGetValue(current, out Rigidbody body))
                {
                    return body;
                }

                current = current.parent;
            }

            return null;
        }

        private static float CalculateCharacterHeight(
            Dictionary<HumanBodyBones, Transform> bones,
            Vector3 up)
        {
            float minimum = float.PositiveInfinity;
            float maximum = float.NegativeInfinity;
            foreach (Transform bone in bones.Values)
            {
                float height = Vector3.Dot(bone.position, up.normalized);
                minimum = Mathf.Min(minimum, height);
                maximum = Mathf.Max(maximum, height);
            }

            float result = maximum - minimum;
            return float.IsNaN(result) || result < 0.5f ? 1.8f : result;
        }

        private static float GetBoneMass(HumanBodyBones role)
        {
            switch (role)
            {
                case HumanBodyBones.Hips:
                    return 12f;
                case HumanBodyBones.Spine:
                case HumanBodyBones.Chest:
                case HumanBodyBones.UpperChest:
                    return 7f;
                case HumanBodyBones.Head:
                    return 5f;
                case HumanBodyBones.LeftUpperLeg:
                case HumanBodyBones.RightUpperLeg:
                    return 7f;
                case HumanBodyBones.LeftLowerLeg:
                case HumanBodyBones.RightLowerLeg:
                    return 5f;
                case HumanBodyBones.LeftUpperArm:
                case HumanBodyBones.RightUpperArm:
                    return 3f;
                case HumanBodyBones.LeftLowerArm:
                case HumanBodyBones.RightLowerArm:
                    return 2f;
                case HumanBodyBones.LeftFoot:
                case HumanBodyBones.RightFoot:
                    return 2f;
                default:
                    return 1f;
            }
        }

        private static float GetMuscleMultiplier(HumanBodyBones role)
        {
            switch (role)
            {
                case HumanBodyBones.Head:
                case HumanBodyBones.Neck:
                    return 0.75f;
                case HumanBodyBones.LeftHand:
                case HumanBodyBones.RightHand:
                case HumanBodyBones.LeftFoot:
                case HumanBodyBones.RightFoot:
                case HumanBodyBones.LeftToes:
                case HumanBodyBones.RightToes:
                    return 0.65f;
                default:
                    return 1f;
            }
        }

        private static Transform CreateChild(Transform parent, string name)
        {
            GameObject child = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(child, "Create " + name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static Transform CreateTrackingTarget(
            Transform parent,
            string name,
            Transform initialPose)
        {
            Transform target = CreateChild(parent, name);
            target.SetPositionAndRotation(initialPose.position, initialPose.rotation);
            return target;
        }

        private static T GetOrAdd<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(gameObject);
        }

        private static T GetOrCreateProfile<T>(string fileName) where T : ScriptableObject
        {
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

        private static GameObject SaveHumanoidPrefab(
            GameObject wrapper,
            string originalName,
            ApexPhysicalHumanoidMode mode)
        {
            EnsureFolder(HumanoidsFolder);
            string safeName = SanitizeFileName(originalName);
            string path = AssetDatabase.GenerateUniqueAssetPath(
                $"{HumanoidsFolder}/{safeName} {mode}.prefab");
            return PrefabUtility.SaveAsPrefabAssetAndConnect(
                wrapper,
                path,
                InteractionMode.UserAction);
        }

        private static void CreateSpawnableCrate(
            GameObject prefab,
            string originalName,
            ApexPhysicalHumanoidMode mode)
        {
            if (prefab == null)
            {
                return;
            }

            EnsureFolder(CratesFolder);
            ApexSpawnableCrate crate = ScriptableObject.CreateInstance<ApexSpawnableCrate>();
            crate.SetTitle(originalName + " " + mode);
            crate.SetDescription("Automatically generated by the Apex 0.1.0 Humanoid Converter.");
            crate.SetTags(new[] { "humanoid", mode.ToString().ToLowerInvariant() });
            crate.SetPrefab(prefab);
            crate.EnsureBarcode();

            string path = AssetDatabase.GenerateUniqueAssetPath(
                $"{CratesFolder}/{SanitizeFileName(originalName)} {mode} Crate.asset");
            AssetDatabase.CreateAsset(crate, path);
            AssetDatabase.SaveAssets();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
            {
                return;
            }

            EnsureFolder(parent);
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static string SanitizeFileName(string value)
        {
            foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalidCharacter, '_');
            }

            return string.IsNullOrWhiteSpace(value) ? "Apex Humanoid" : value;
        }
    }
}
