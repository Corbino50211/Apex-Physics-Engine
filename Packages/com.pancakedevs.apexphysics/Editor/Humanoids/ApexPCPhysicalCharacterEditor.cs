using PancakeDevs.ApexPhysics;
using UnityEditor;
using UnityEngine;

namespace PancakeDevs.ApexPhysics.Editor
{
    [CustomEditor(typeof(ApexPCPhysicalCharacter))]
    public sealed class ApexPCPhysicalCharacterEditor : UnityEditor.Editor
    {
        private SerializedProperty movementSpeed;
        private SerializedProperty acceleration;
        private SerializedProperty braking;
        private SerializedProperty turnSpeed;
        private SerializedProperty motorMass;
        private SerializedProperty minimumImpactSpeed;
        private SerializedProperty minimumImpactImpulse;
        private SerializedProperty startupImpactDelay;
        private SerializedProperty automaticallyGetUp;
        private SerializedProperty ragdollDuration;
        private SerializedProperty getUpDuration;
        private bool showAnimatorParameters;

        private void OnEnable()
        {
            movementSpeed = serializedObject.FindProperty("movementSpeed");
            acceleration = serializedObject.FindProperty("acceleration");
            braking = serializedObject.FindProperty("braking");
            turnSpeed = serializedObject.FindProperty("turnSpeed");
            motorMass = serializedObject.FindProperty("motorMass");
            minimumImpactSpeed = serializedObject.FindProperty("minimumRagdollImpactSpeed");
            minimumImpactImpulse = serializedObject.FindProperty("minimumRagdollImpulse");
            startupImpactDelay = serializedObject.FindProperty("startupImpactDelay");
            automaticallyGetUp = serializedObject.FindProperty("automaticallyGetUp");
            ragdollDuration = serializedObject.FindProperty("ragdollDuration");
            getUpDuration = serializedObject.FindProperty("getUpDuration");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            ApexPCPhysicalCharacter character = (ApexPCPhysicalCharacter)target;

            EditorGUILayout.LabelField("Apex PC Physical Character", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "PC-first character controller. VR tracking and physical-player setup are intentionally disabled until the desktop NPC system is stable.",
                MessageType.Info);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Movement", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(movementSpeed);
            EditorGUILayout.PropertyField(acceleration);
            EditorGUILayout.PropertyField(braking);
            EditorGUILayout.PropertyField(turnSpeed);
            EditorGUILayout.PropertyField(motorMass);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Impact Ragdoll", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(minimumImpactSpeed, new GUIContent("Minimum Impact Speed"));
            EditorGUILayout.PropertyField(minimumImpactImpulse, new GUIContent("Minimum Impact Impulse"));
            EditorGUILayout.PropertyField(startupImpactDelay, new GUIContent("Startup Arming Delay"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Recovery", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(automaticallyGetUp);
            EditorGUILayout.PropertyField(ragdollDuration);
            EditorGUILayout.PropertyField(getUpDuration);

            showAnimatorParameters = EditorGUILayout.Foldout(
                showAnimatorParameters,
                "Animator Parameters",
                true);
            if (showAnimatorParameters)
            {
                EditorGUI.indentLevel++;
                DrawProperty("speedParameter");
                DrawProperty("movingParameter");
                DrawProperty("groundedParameter");
                DrawProperty("ragdollParameter");
                DrawProperty("getUpFrontTrigger");
                DrawProperty("getUpBackTrigger");
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Generated Runtime", EditorStyles.boldLabel);
            EditorGUILayout.ObjectField("Motor Body", character.MotorBody, typeof(Rigidbody), true);
            EditorGUILayout.ObjectField("Motor Collider", character.MotorCollider, typeof(CapsuleCollider), true);
            EditorGUILayout.Toggle("Ready", character.IsReady);
            EditorGUILayout.EnumPopup("State", character.State);
            EditorGUILayout.Toggle("Grounded", character.Grounded);

            DrawLocomotionDiagnostics(character);

            if (!Application.isPlaying)
            {
                if (GUILayout.Button("Rebuild PC Physical Character", GUILayout.Height(34f)))
                {
                    Undo.RecordObject(character, "Rebuild Apex PC Physical Character");
                    character.Rebuild();
                    character.Humanoid?.EnsurePCPhysicalCharacter();
                    EditorUtility.SetDirty(character);
                }

                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Idle"))
                {
                    character.SetIdle();
                }

                if (GUILayout.Button("Wander"))
                {
                    character.SetWander();
                }

                if (GUILayout.Button("Stagger"))
                {
                    character.Stagger();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Knock Down (Keep Momentum)"))
                {
                    character.KnockDown();
                }

                if (GUILayout.Button("Get Up"))
                {
                    character.BeginGetUp();
                }

                if (GUILayout.Button("Activate"))
                {
                    character.Activate();
                }
            }

            Repaint();
        }

        private static void DrawLocomotionDiagnostics(ApexPCPhysicalCharacter character)
        {
            ApexPhysicalHumanoid humanoid = character.Humanoid;
            if (humanoid == null)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Grounded Locomotion", EditorStyles.boldLabel);
            EditorGUILayout.ObjectField(
                "Foot Planting",
                humanoid.PCFootPlanting,
                typeof(ApexPCFootPlanting),
                true);
            EditorGUILayout.Toggle(
                "Feet Ready",
                humanoid.PCFootPlanting != null && humanoid.PCFootPlanting.IsReady);
            EditorGUILayout.ObjectField(
                "Navigation Bridge",
                humanoid.PCNavigationDriver,
                typeof(ApexPCNavigationDriver),
                true);
            EditorGUILayout.ObjectField(
                "Momentum Handoff",
                humanoid.PCRagdollMomentum,
                typeof(ApexPCRagdollMomentum),
                true);

            ApexNPCNavigator navigator = humanoid.NPCNavigator;
            if (navigator == null)
            {
                return;
            }

            EditorGUILayout.Toggle("On NavMesh", navigator.IsOnNavMesh);
            EditorGUILayout.Toggle("Has Destination", navigator.HasDestination);
            EditorGUILayout.Toggle("Has Complete Path", navigator.HasCompletePath);
            EditorGUILayout.EnumPopup("Path Status", navigator.PathStatus);
            EditorGUILayout.FloatField("Remaining Distance", navigator.RemainingDistance);
        }

        private void DrawProperty(string propertyName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property);
            }
        }
    }
}
