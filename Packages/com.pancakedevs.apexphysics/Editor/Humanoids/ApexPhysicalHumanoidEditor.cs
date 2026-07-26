using PancakeDevs.ApexPhysics;
using UnityEditor;
using UnityEngine;

namespace PancakeDevs.ApexPhysics.Editor
{
    [CustomEditor(typeof(ApexPhysicalHumanoid))]
    public sealed class ApexPhysicalHumanoidEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            ApexPhysicalHumanoid humanoid = (ApexPhysicalHumanoid)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Apex Physical Humanoid", EditorStyles.boldLabel);

            if (humanoid.TargetAnimator == null || !humanoid.TargetAnimator.isHuman)
            {
                EditorGUILayout.HelpBox(
                    "The hidden target Animator is missing or no longer uses a valid Humanoid Avatar.",
                    MessageType.Error);
            }

            if (humanoid.ActiveRagdoll == null || humanoid.PhysicalHips == null)
            {
                EditorGUILayout.HelpBox(
                    "The active-ragdoll controller or physical hips reference is missing.",
                    MessageType.Error);
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Enter Play Mode to use the physical test controls.",
                    MessageType.Info);
                return;
            }

            DrawRagdollControls(humanoid);

            if (humanoid.Mode == ApexPhysicalHumanoidMode.PhysicalPlayer)
            {
                DrawPlayerControls(humanoid);
            }
            else if (humanoid.Mode == ApexPhysicalHumanoidMode.PhysicalNPC)
            {
                DrawNpcControls(humanoid);
            }

            Repaint();
        }

        private static void DrawRagdollControls(ApexPhysicalHumanoid humanoid)
        {
            ApexActiveRagdoll ragdoll = humanoid.ActiveRagdoll;
            if (ragdoll == null)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Ragdoll", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("State", ragdoll.State.ToString());
            EditorGUILayout.Slider("Recovery", ragdoll.RecoveryProgress, 0f, 1f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Knock Down"))
                {
                    ragdoll.KnockDown();
                }

                if (GUILayout.Button("Recover"))
                {
                    ragdoll.BeginRecovery();
                }

                if (GUILayout.Button("Activate"))
                {
                    ragdoll.RecoverImmediately();
                }
            }
        }

        private static void DrawPlayerControls(ApexPhysicalHumanoid humanoid)
        {
            ApexHumanoidPlayerMotor motor = humanoid.HumanoidPlayerMotor;
            if (motor == null)
            {
                EditorGUILayout.HelpBox(
                    "The full-body player motor is missing.",
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Physical Player", EditorStyles.boldLabel);
            EditorGUILayout.Toggle("Grounded", motor.IsGrounded);
            EditorGUILayout.Vector3Field("Velocity", motor.Rigidbody.velocity);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Forward"))
                {
                    motor.SetMoveInput(Vector2.up);
                }

                if (GUILayout.Button("Stop"))
                {
                    motor.ClearInput();
                }

                if (GUILayout.Button("Jump"))
                {
                    motor.RequestJump();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Snap Left"))
                {
                    motor.SnapTurn(-45f);
                }

                if (GUILayout.Button("Snap Right"))
                {
                    motor.SnapTurn(45f);
                }
            }
        }

        private static void DrawNpcControls(ApexPhysicalHumanoid humanoid)
        {
            ApexNPCBrain brain = humanoid.NPCBrain;
            if (brain == null)
            {
                EditorGUILayout.HelpBox("The NPC brain is missing.", MessageType.Warning);
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Physical NPC", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Mode", brain.CurrentMode.ToString());

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Idle"))
                {
                    brain.SetMode(ApexNPCBehaviorMode.Idle);
                }

                if (GUILayout.Button("Wander"))
                {
                    brain.SetMode(ApexNPCBehaviorMode.Wander);
                }

                if (GUILayout.Button("New Destination"))
                {
                    brain.PickNewWanderDestination();
                }
            }
        }
    }
}
