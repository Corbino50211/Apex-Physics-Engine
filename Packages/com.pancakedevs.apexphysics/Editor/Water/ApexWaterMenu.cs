using PancakeDevs.ApexPhysics;
using UnityEditor;
using UnityEngine;

namespace PancakeDevs.ApexPhysics.Editor
{
    public static class ApexWaterMenu
    {
        private const string Root = "Apex Physics Engine/Water/";

        [MenuItem(Root + "Create Water Volume", false, 600)]
        public static void CreateWaterVolume()
        {
            GameObject water = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(water, "Create Apex Water Volume");
            water.name = "Apex Water Volume";
            water.transform.position = Vector3.zero;
            water.transform.localScale = new Vector3(20f, 4f, 20f);

            ApexWaterVolume volume = Undo.AddComponent<ApexWaterVolume>(water);
            volume.CaptureBoundsFromCurrentMesh();
            volume.RefreshBounds();
            ApexWaterAssetUtility.GenerateSurfaceAssets(
                volume,
                ApexWaterAssetUtility.HasUrpSampleShader(),
                out string message);
            Selection.activeGameObject = water;
            Debug.Log(message, water);
        }

        [MenuItem(Root + "Make Selected Rigidbody Buoyant", false, 610)]
        public static void MakeSelectedBuoyant()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                return;
            }

            if (selected.GetComponent<Rigidbody>() == null)
            {
                Undo.AddComponent<Rigidbody>(selected);
            }
            ApexBuoyantBody buoyant = selected.GetComponent<ApexBuoyantBody>();
            if (buoyant == null)
            {
                buoyant = Undo.AddComponent<ApexBuoyantBody>(selected);
            }
            buoyant.GeneratePointsFromColliders(true);
            EditorUtility.SetDirty(buoyant);
            Selection.activeGameObject = selected;
        }

        [MenuItem(Root + "Make Selected Rigidbody Buoyant", true)]
        private static bool ValidateMakeSelectedBuoyant()
        {
            return Selection.activeGameObject != null;
        }

        [MenuItem(Root + "Make Selected Rigidbody a Swimmer", false, 620)]
        public static void MakeSelectedSwimmer()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                return;
            }

            if (selected.GetComponent<Rigidbody>() == null)
            {
                Undo.AddComponent<Rigidbody>(selected);
            }
            if (selected.GetComponent<ApexSwimmer>() == null)
            {
                Undo.AddComponent<ApexSwimmer>(selected);
            }
            Selection.activeGameObject = selected;
        }

        [MenuItem(Root + "Make Selected Rigidbody a Swimmer", true)]
        private static bool ValidateMakeSelectedSwimmer()
        {
            return Selection.activeGameObject != null;
        }

        [MenuItem(Root + "Create Example Setup", false, 650)]
        public static void CreateExampleSetup()
        {
            CreateWaterVolume();
            GameObject water = Selection.activeGameObject;
            water.name = "Apex Water Example Lake";

            GameObject floatingBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(floatingBox, "Create Apex Floating Box");
            floatingBox.name = "Apex Floating Box";
            floatingBox.transform.position = new Vector3(3f, 2.5f, 3f);
            floatingBox.transform.localScale = new Vector3(1.5f, 0.75f, 2.5f);
            Rigidbody boxBody = Undo.AddComponent<Rigidbody>(floatingBox);
            boxBody.mass = 25f;
            ApexBuoyantBody buoyant = Undo.AddComponent<ApexBuoyantBody>(floatingBox);
            buoyant.GeneratePointsFromColliders(true);

            GameObject swimmerObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Undo.RegisterCreatedObjectUndo(swimmerObject, "Create Apex Swimmer");
            swimmerObject.name = "Apex Example Swimmer";
            swimmerObject.transform.position = new Vector3(-3f, 2.5f, -3f);
            Rigidbody swimmerBody = Undo.AddComponent<Rigidbody>(swimmerObject);
            swimmerBody.constraints =
                RigidbodyConstraints.FreezeRotationX |
                RigidbodyConstraints.FreezeRotationZ;
            Undo.AddComponent<ApexSwimmer>(swimmerObject);

            Selection.activeGameObject = water;
            Debug.Log(
                "Created Apex water example: lake, buoyant box, and an input-agnostic swimmer. " +
                "Feed ApexSwimmer from your Input System actions, AI, networking, or VR controls.",
                water);
        }
    }
}
