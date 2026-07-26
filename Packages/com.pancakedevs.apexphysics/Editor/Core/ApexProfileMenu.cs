using PancakeDevs.ApexPhysics;
using UnityEditor;

namespace PancakeDevs.ApexPhysics.Editor
{
    internal static class ApexProfileMenu
    {
        private const string CreatePhysicsProfilePath = "Apex Physics Engine/Profiles/Create Physics Profile";
        private const string CreateGrabProfilePath = "Apex Physics Engine/Profiles/Create Grab Profile";

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
    }
}
