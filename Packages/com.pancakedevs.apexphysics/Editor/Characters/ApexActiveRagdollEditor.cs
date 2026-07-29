using System.Collections.Generic;
using PancakeDevs.ApexPhysics;
using UnityEditor;
using UnityEngine;

namespace PancakeDevs.ApexPhysics.Editor
{
    [CustomEditor(typeof(ApexActiveRagdoll))]
    public sealed class ApexActiveRagdollEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            ApexActiveRagdoll ragdoll = (ApexActiveRagdoll)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Apex Active Ragdoll", EditorStyles.boldLabel);

            if (ragdoll.Profile == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign an Apex Ragdoll Profile before testing muscles or recovery.",
                    MessageType.Warning);
            }

            if (ragdoll.Bones.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No ApexRagdollBone components were found below this controller.",
                    MessageType.Warning);
            }

            if (ragdoll.RootBone == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign one ragdoll bone as the root so the physical body can follow the animated root position.",
                    MessageType.Warning);
            }

            EditorGUILayout.HelpBox(
                "The physical skeleton and animated target skeleton must be separate hierarchies with matching local bone axes.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh Bones"))
                {
                    Undo.RecordObject(ragdoll, "Refresh Apex Ragdoll Bones");
                    ragdoll.RefreshBones();
                    EditorUtility.SetDirty(ragdoll);
                }

                if (GUILayout.Button("Capture Current Pose"))
                {
                    ragdoll.CaptureCurrentPose();
                }
            }

            using (new EditorGUI.DisabledScope(ragdoll.TargetAnimator == null || ragdoll.Bones.Count == 0))
            {
                if (GUILayout.Button("Map Targets by Bone Name"))
                {
                    MapTargetsByName(ragdoll);
                }
            }

            if (!Application.isPlaying)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("State", ragdoll.State.ToString());
            EditorGUILayout.Slider("Recovery Progress", ragdoll.RecoveryProgress, 0f, 1f);
            EditorGUILayout.Slider("Global Strength", ragdoll.GlobalStrength, 0f, 1f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Activate"))
                {
                    ragdoll.RecoverImmediately();
                }

                if (GUILayout.Button("Knock Down"))
                {
                    ragdoll.KnockDown();
                }

                if (GUILayout.Button("Recover"))
                {
                    ragdoll.BeginRecovery();
                }
            }

            Repaint();
        }

        private static void MapTargetsByName(ApexActiveRagdoll ragdoll)
        {
            Transform[] targetTransforms = ragdoll.TargetAnimator.GetComponentsInChildren<Transform>(true);
            Dictionary<string, Transform> targetsByName = new Dictionary<string, Transform>();
            for (int i = 0; i < targetTransforms.Length; i++)
            {
                Transform targetTransform = targetTransforms[i];
                if (targetTransform != null && !targetsByName.ContainsKey(targetTransform.name))
                {
                    targetsByName.Add(targetTransform.name, targetTransform);
                }
            }

            int mappedCount = 0;
            for (int i = 0; i < ragdoll.Bones.Count; i++)
            {
                ApexRagdollBone bone = ragdoll.Bones[i];
                if (bone == null || !targetsByName.TryGetValue(bone.name, out Transform targetTransform))
                {
                    continue;
                }

                Undo.RecordObject(bone, "Map Apex Ragdoll Target");
                bone.SetTarget(targetTransform);
                EditorUtility.SetDirty(bone);
                mappedCount++;
            }

            ragdoll.CaptureCurrentPose();
            Debug.Log($"Apex mapped {mappedCount} ragdoll bone target(s) by name.", ragdoll);
        }
    }
}
