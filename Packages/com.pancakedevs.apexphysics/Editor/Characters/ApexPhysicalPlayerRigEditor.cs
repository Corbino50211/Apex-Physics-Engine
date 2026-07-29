using PancakeDevs.ApexPhysics;
using UnityEditor;
using UnityEngine;

namespace PancakeDevs.ApexPhysics.Editor
{
    [CustomEditor(typeof(ApexPhysicalPlayerRig))]
    public sealed class ApexPhysicalPlayerRigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            ApexPhysicalPlayerRig rig = (ApexPhysicalPlayerRig)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Apex Physical Player", EditorStyles.boldLabel);

            if (rig.Profile == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign an Apex Physical Player Profile before testing movement.",
                    MessageType.Warning);
            }

            if (rig.HeadTarget == null)
            {
                EditorGUILayout.HelpBox(
                    "No head target is assigned. Crouching can still be controlled manually, but tracked head height and head orientation are unavailable.",
                    MessageType.Warning);
            }

            EditorGUILayout.HelpBox(
                "This component does not read Input System or XR controls directly. Call SetMoveInput, SetTurnInput, RequestJump, and SetCrouch from an adapter.",
                MessageType.Info);

            if (GUILayout.Button("Apply Player Profile"))
            {
                Undo.RecordObject(rig.gameObject, "Apply Apex Player Profile");
                rig.ApplyProfile();
                EditorUtility.SetDirty(rig.gameObject);
            }

            if (!Application.isPlaying)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Grounded", rig.IsGrounded ? "Yes" : "No");
            EditorGUILayout.Vector3Field("Ground Normal", rig.GroundNormal);
            EditorGUILayout.Vector3Field("Horizontal Velocity", rig.HorizontalVelocity);
            EditorGUILayout.Vector2Field("Move Input", rig.MovementInput);
            EditorGUILayout.FloatField("Turn Input", rig.TurnInput);
            EditorGUILayout.LabelField("Crouching", rig.IsCrouching ? "Yes" : "No");

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Jump"))
                {
                    rig.RequestJump();
                }

                if (GUILayout.Button("Turn -45"))
                {
                    rig.SnapTurn(-45f);
                }

                if (GUILayout.Button("Turn +45"))
                {
                    rig.SnapTurn(45f);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(rig.IsCrouching ? "Stand" : "Crouch"))
                {
                    rig.SetCrouch(!rig.IsCrouching);
                }

                if (GUILayout.Button("Move Forward"))
                {
                    rig.SetMoveInput(Vector2.up);
                }

                if (GUILayout.Button("Stop Input"))
                {
                    rig.ClearInput();
                }
            }

            Repaint();
        }
    }
}
