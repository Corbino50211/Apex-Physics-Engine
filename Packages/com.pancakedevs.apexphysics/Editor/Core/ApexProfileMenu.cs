using PancakeDevs.ApexPhysics;
using UnityEditor;

namespace PancakeDevs.ApexPhysics.Editor
{
    internal static class ApexProfileMenu
    {
        private const string CreatePhysicsProfilePath = "Apex Physics Engine/Profiles/Create Physics Profile";
        private const string CreateGrabProfilePath = "Apex Physics Engine/Profiles/Create Grab Profile";
        private const string CreateRagdollProfilePath = "Apex Physics Engine/Profiles/Create Ragdoll Profile";
        private const string CreatePlayerProfilePath = "Apex Physics Engine/Profiles/Create Physical Player Profile";

        [MenuItem(CreatePhysicsProfilePath, false, 40)]
        private static void CreatePhysicsProfile()
        {
            ProjectWindowUtil.CreateAsset(
                UnityEngine.ScriptableObject.CreateInstance<ApexPhysicsProfile>(),
                "Apex Physics Profile.asset");
        }

        [MenuItem(CreateGrabProfilePath, false, 41)]
        private static void CreateGrabProfile()
        {
            ProjectWindowUtil.CreateAsset(
                UnityEngine.ScriptableObject.CreateInstance<ApexGrabProfile>(),
                "Apex Grab Profile.asset");
        }

        [MenuItem(CreateRagdollProfilePath, false, 42)]
        private static void CreateRagdollProfile()
        {
            ProjectWindowUtil.CreateAsset(
                UnityEngine.ScriptableObject.CreateInstance<ApexRagdollProfile>(),
                "Apex Ragdoll Profile.asset");
        }

        [MenuItem(CreatePlayerProfilePath, false, 43)]
        private static void CreatePhysicalPlayerProfile()
        {
            ProjectWindowUtil.CreateAsset(
                UnityEngine.ScriptableObject.CreateInstance<ApexPhysicalPlayerProfile>(),
                "Apex Physical Player Profile.asset");
        }
    }
}
