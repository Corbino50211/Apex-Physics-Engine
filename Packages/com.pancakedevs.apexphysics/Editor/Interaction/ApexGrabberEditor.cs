using PancakeDevs.ApexPhysics;
using UnityEditor;
using UnityEngine;

namespace PancakeDevs.ApexPhysics.Editor
{
    [CustomEditor(typeof(ApexGrabber))]
    public sealed class ApexGrabberEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            ApexGrabber grabber = (ApexGrabber)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Apex Grab Tools", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Enter Play Mode to test grabbing from this inspector. No legacy or new Input System dependency is required.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.ObjectField(
                "Held Object",
                grabber.HeldObject,
                typeof(ApexGrabbable),
                true);
            EditorGUILayout.Vector3Field("Sampled Velocity", grabber.SampledLinearVelocity);
            EditorGUILayout.Vector3Field("Sampled Angular Velocity", grabber.SampledAngularVelocity);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(grabber.IsHolding))
                {
                    if (GUILayout.Button("Grab Closest"))
                    {
                        grabber.TryGrabClosest();
                    }
                }

                using (new EditorGUI.DisabledScope(!grabber.IsHolding))
                {
                    if (GUILayout.Button("Release / Throw"))
                    {
                        grabber.Release();
                    }
                }
            }
        }
    }
}