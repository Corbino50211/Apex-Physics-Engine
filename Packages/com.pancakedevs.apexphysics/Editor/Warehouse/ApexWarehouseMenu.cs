using System.Collections.Generic;
using System.IO;
using PancakeDevs.ApexPhysics;
using UnityEditor;
using UnityEngine;

namespace PancakeDevs.ApexPhysics.Editor
{
    internal static class ApexWarehouseMenu
    {
        private const string Root = "Apex Physics Engine/Warehouse/";
        private const string WarehouseFolder = "Assets/Apex Physics Engine/Warehouse";
        private const string CratesFolder = WarehouseFolder + "/Crates";
        private const string PalletsFolder = WarehouseFolder + "/Pallets";

        [MenuItem(Root + "Create Warehouse", false, 10)]
        private static void CreateWarehouse()
        {
            ApexWarehouse existing = Object.FindFirstObjectByType<ApexWarehouse>();
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing.gameObject);
                return;
            }

            GameObject warehouseObject = new GameObject("Apex Warehouse");
            Undo.RegisterCreatedObjectUndo(warehouseObject, "Create Apex Warehouse");
            Undo.AddComponent<ApexWarehouse>(warehouseObject);
            Selection.activeGameObject = warehouseObject;
        }

        [MenuItem(Root + "Create Pallet", false, 20)]
        private static void CreatePallet()
        {
            EnsureFolder(PalletsFolder);
            ApexPallet pallet = ScriptableObject.CreateInstance<ApexPallet>();
            pallet.SetTitle("Apex Pallet");
            pallet.EnsureBarcode();

            string path = AssetDatabase.GenerateUniqueAssetPath(PalletsFolder + "/Apex Pallet.asset");
            AssetDatabase.CreateAsset(pallet, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = pallet;
            EditorGUIUtility.PingObject(pallet);
        }

        [MenuItem(Root + "Create Spawnable Crate From Selected Prefab", false, 21)]
        private static void CreateSpawnableCrateFromSelectedPrefab()
        {
            GameObject prefab = Selection.activeObject as GameObject;
            if (!IsPrefabAsset(prefab))
            {
                return;
            }

            EnsureFolder(CratesFolder);
            ApexSpawnableCrate crate = ScriptableObject.CreateInstance<ApexSpawnableCrate>();
            crate.SetTitle(prefab.name);
            crate.SetPrefab(prefab);
            crate.EnsureBarcode();

            string fileName = SanitizeFileName(prefab.name) + " Crate.asset";
            string path = AssetDatabase.GenerateUniqueAssetPath(CratesFolder + "/" + fileName);
            AssetDatabase.CreateAsset(crate, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = crate;
            EditorGUIUtility.PingObject(crate);
        }

        [MenuItem(Root + "Create Pallet From Selected Crates", false, 22)]
        private static void CreatePalletFromSelectedCrates()
        {
            List<ApexCrate> selectedCrates = GetSelectedCrates();
            if (selectedCrates.Count == 0)
            {
                return;
            }

            EnsureFolder(PalletsFolder);
            ApexPallet pallet = ScriptableObject.CreateInstance<ApexPallet>();
            pallet.SetTitle("Apex Pallet");
            pallet.SetCrates(selectedCrates);
            pallet.EnsureBarcode();

            string path = AssetDatabase.GenerateUniqueAssetPath(PalletsFolder + "/Apex Pallet.asset");
            AssetDatabase.CreateAsset(pallet, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = pallet;
            EditorGUIUtility.PingObject(pallet);
        }

        [MenuItem(Root + "Create Crate Spawner", false, 30)]
        private static void CreateCrateSpawner()
        {
            GameObject spawnerObject = new GameObject("Apex Crate Spawner");
            Undo.RegisterCreatedObjectUndo(spawnerObject, "Create Apex Crate Spawner");

            GameObject selectedSceneObject = Selection.activeGameObject;
            if (selectedSceneObject != null && !EditorUtility.IsPersistent(selectedSceneObject))
            {
                spawnerObject.transform.SetParent(selectedSceneObject.transform, false);
            }
            else
            {
                spawnerObject.transform.position = SceneView.lastActiveSceneView != null
                    ? SceneView.lastActiveSceneView.pivot
                    : Vector3.zero;
            }

            ApexCrateSpawner spawner = Undo.AddComponent<ApexCrateSpawner>(spawnerObject);
            if (Selection.activeObject is ApexSpawnableCrate selectedCrate)
            {
                spawner.SetCrate(selectedCrate);
            }

            Selection.activeGameObject = spawnerObject;
            EditorGUIUtility.PingObject(spawnerObject);
        }

        [MenuItem(Root + "Create Spawnable Crate From Selected Prefab", true)]
        private static bool ValidateCreateSpawnableCrateFromSelectedPrefab()
        {
            return IsPrefabAsset(Selection.activeObject as GameObject);
        }

        [MenuItem(Root + "Create Pallet From Selected Crates", true)]
        private static bool ValidateCreatePalletFromSelectedCrates()
        {
            return GetSelectedCrates().Count > 0;
        }

        private static bool IsPrefabAsset(GameObject candidate)
        {
            return candidate != null &&
                   EditorUtility.IsPersistent(candidate) &&
                   PrefabUtility.GetPrefabAssetType(candidate) != PrefabAssetType.NotAPrefab;
        }

        private static List<ApexCrate> GetSelectedCrates()
        {
            List<ApexCrate> crates = new List<ApexCrate>();
            Object[] selectedObjects = Selection.objects;
            for (int i = 0; i < selectedObjects.Length; i++)
            {
                if (selectedObjects[i] is ApexCrate crate && !crates.Contains(crate))
                {
                    crates.Add(crate);
                }
            }

            return crates;
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

            return string.IsNullOrWhiteSpace(value) ? "Spawnable" : value;
        }
    }
}
