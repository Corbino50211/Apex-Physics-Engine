using PancakeDevs.ApexPhysics;
using UnityEditor;
using UnityEngine;

namespace PancakeDevs.ApexPhysics.Editor
{
    public sealed class ApexDestructionWindow : EditorWindow
    {
        private const string MenuPath =
            "Apex Physics Engine/Destruction/Fracture Selected Mesh...";

        [SerializeField] private GameObject targetObject;
        [SerializeField, Range(2, 64)] private int chunkCount = 12;
        [SerializeField] private int randomSeed = 5021;
        [SerializeField, Min(0f)] private float minimumChunkVolume = 0.0001f;
        [SerializeField, Range(0f, 1f)] private float planeJitter = 0.55f;
        [SerializeField, Min(0.001f)] private float interiorUvScale = 1f;
        [SerializeField] private Material interiorMaterial;

        private string lastMessage = string.Empty;
        private MessageType lastMessageType = MessageType.None;

        [MenuItem(MenuPath, false, 500)]
        private static void OpenFromMenu()
        {
            OpenFor(Selection.activeGameObject);
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateOpenFromMenu()
        {
            GameObject selected = Selection.activeGameObject;
            return selected != null && selected.GetComponent<MeshFilter>() != null;
        }

        public static void OpenFor(GameObject target)
        {
            ApexDestructionWindow window = GetWindow<ApexDestructionWindow>();
            window.titleContent = new GUIContent("Apex Destruction");
            window.minSize = new Vector2(430f, 430f);
            window.targetObject = target;
            window.Show();
            window.Focus();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Apex Destruction");
            minSize = new Vector2(430f, 430f);
            if (targetObject == null && Selection.activeGameObject != null)
            {
                targetObject = Selection.activeGameObject;
            }
        }

        private void OnSelectionChange()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected != null && selected.GetComponent<MeshFilter>() != null)
            {
                targetObject = selected;
                Repaint();
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Apex Destruction Foundation", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Slices a closed mesh into editor-generated chunks, creates interior cap polygons, and wires the object for impact-driven runtime breakage.",
                EditorStyles.wordWrappedLabel);

            EditorGUILayout.Space(10f);
            targetObject = (GameObject)EditorGUILayout.ObjectField(
                "Target Object",
                targetObject,
                typeof(GameObject),
                true);

            DrawTargetStatus();

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Fracture Geometry", EditorStyles.boldLabel);
                chunkCount = EditorGUILayout.IntSlider("Target Chunk Count", chunkCount, 2, 64);
                randomSeed = EditorGUILayout.IntField("Random Seed", randomSeed);
                minimumChunkVolume = EditorGUILayout.FloatField(
                    "Minimum Chunk Volume",
                    minimumChunkVolume);
                planeJitter = EditorGUILayout.Slider("Fracture Irregularity", planeJitter, 0f, 1f);
                interiorUvScale = EditorGUILayout.FloatField("Interior UV Scale", interiorUvScale);
                interiorMaterial = (Material)EditorGUILayout.ObjectField(
                    "Interior Material",
                    interiorMaterial,
                    typeof(Material),
                    false);

                EditorGUILayout.HelpBox(
                    "The source should be a closed, manifold MeshFilter mesh. Open or paper-thin meshes may not produce valid capped chunks.",
                    MessageType.Info);
            }

            if (!string.IsNullOrWhiteSpace(lastMessage))
            {
                EditorGUILayout.Space(8f);
                EditorGUILayout.HelpBox(lastMessage, lastMessageType);
            }

            GUILayout.FlexibleSpace();
            DrawActions();
            EditorGUILayout.Space(10f);
        }

        private void DrawTargetStatus()
        {
            if (targetObject == null)
            {
                EditorGUILayout.HelpBox("Select a scene object with a MeshFilter and MeshRenderer.", MessageType.Warning);
                return;
            }

            MeshFilter meshFilter = targetObject.GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = targetObject.GetComponent<MeshRenderer>();
            if (meshFilter == null || meshFilter.sharedMesh == null || meshRenderer == null)
            {
                EditorGUILayout.HelpBox(
                    "The target needs a MeshFilter with a mesh and a MeshRenderer on the same GameObject.",
                    MessageType.Error);
                return;
            }

            ApexDestructible destructible = targetObject.GetComponent<ApexDestructible>();
            string generated = destructible != null && destructible.HasGeneratedFracture
                ? destructible.ChunkCount + " generated chunks"
                : "No generated fracture";

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Mesh", meshFilter.sharedMesh.name);
                EditorGUILayout.LabelField("Vertices", meshFilter.sharedMesh.vertexCount.ToString());
                EditorGUILayout.LabelField("Submeshes", meshFilter.sharedMesh.subMeshCount.ToString());
                EditorGUILayout.LabelField("Current State", generated);
            }
        }

        private void DrawActions()
        {
            bool canGenerate = CanGenerate();
            using (new EditorGUI.DisabledScope(!canGenerate || EditorApplication.isPlaying))
            {
                if (GUILayout.Button("Generate / Rebuild Fracture", GUILayout.Height(40f)))
                {
                    Generate();
                }
            }

            ApexDestructible destructible = targetObject != null
                ? targetObject.GetComponent<ApexDestructible>()
                : null;
            using (new EditorGUI.DisabledScope(
                       destructible == null ||
                       !destructible.HasGeneratedFracture ||
                       EditorApplication.isPlaying))
            {
                if (GUILayout.Button("Clear Generated Fracture"))
                {
                    bool confirmed = EditorUtility.DisplayDialog(
                        "Clear Apex Fracture",
                        "Remove the generated chunk hierarchy and its generated mesh assets?",
                        "Clear",
                        "Cancel");
                    if (confirmed)
                    {
                        ApexDestructionGenerator.ClearGeneratedFracture(destructible, true);
                        lastMessage = "Cleared the generated Apex fracture.";
                        lastMessageType = MessageType.Info;
                    }
                }
            }
        }

        private bool CanGenerate()
        {
            return targetObject != null &&
                   targetObject.GetComponent<MeshFilter>()?.sharedMesh != null &&
                   targetObject.GetComponent<MeshRenderer>() != null;
        }

        private void Generate()
        {
            minimumChunkVolume = Mathf.Max(0f, minimumChunkVolume);
            interiorUvScale = Mathf.Max(0.001f, interiorUvScale);

            try
            {
                EditorUtility.DisplayProgressBar(
                    "Apex Destruction",
                    "Slicing mesh and generating capped chunks...",
                    0.35f);

                bool success = ApexDestructionGenerator.Generate(
                    targetObject,
                    chunkCount,
                    randomSeed,
                    minimumChunkVolume,
                    planeJitter,
                    interiorUvScale,
                    interiorMaterial,
                    out string message);

                lastMessage = message;
                lastMessageType = success ? MessageType.Info : MessageType.Error;
                if (success)
                {
                    Debug.Log(message, targetObject);
                }
                else
                {
                    Debug.LogWarning(message, targetObject);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
    }
}
