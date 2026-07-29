using UnityEngine;
using UnityEngine.SceneManagement;

namespace PancakeDevs.ApexPhysics
{
    internal static class ApexClimbingBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallOnExistingRigs()
        {
            InstallInScene(SceneManager.GetActiveScene());
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            InstallInScene(scene);
        }

        private static void InstallInScene(Scene scene)
        {
            ApexPhysicalOpenXRBody[] bodies = Object.FindObjectsByType<ApexPhysicalOpenXRBody>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (ApexPhysicalOpenXRBody body in bodies)
            {
                if (body == null || body.gameObject.scene != scene)
                {
                    continue;
                }

                if (body.GetComponent<ApexPhysicalVRClimber>() == null)
                {
                    body.gameObject.AddComponent<ApexPhysicalVRClimber>();
                }
            }
        }
    }
}
