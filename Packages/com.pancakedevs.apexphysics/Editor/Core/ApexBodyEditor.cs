using PancakeDevs.ApexPhysics;
using UnityEditor;
using UnityEngine;

namespace PancakeDevs.ApexPhysics.Editor
{
    [CustomEditor(typeof(ApexBody))]
    public sealed class ApexBodyEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            ApexBody body = (ApexBody)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Apex Tools", EditorStyles.boldLabel);

            if (body.Profile == null)
            {
                EditorGUILayout.HelpBox(
                    "No Apex Physics Profile is assigned. The Rigidbody can still be used, but reusable profile settings will not be applied.",
                    MessageType.Info);
            }

            using (new EditorGUI.DisabledScope(body.Profile == null))
            {
                if (GUILayout.Button("Apply Physics Profile"))
                {
                    Undo.RecordObject(body.Rigidbody, "Apply Apex Physics Profile");
                    body.ApplyProfile();
                    EditorUtility.SetDirty(body.Rigidbody);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Wake"))
                {
                    body.Wake();
                }

                if (GUILayout.Button("Stop Motion"))
                {
                    Undo.RecordObject(body.Rigidbody, "Stop Apex Body Motion");
                    body.StopMotion();
                }
            }

            if (Application.isPlaying)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Runtime", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Mass", body.Mass.ToString("0.###"));
                EditorGUILayout.LabelField("Sleeping", body.IsSleeping ? "Yes" : "No");
                EditorGUILayout.Vector3Field("Center of Mass", body.WorldCenterOfMass);
            }
        }
    }
}
