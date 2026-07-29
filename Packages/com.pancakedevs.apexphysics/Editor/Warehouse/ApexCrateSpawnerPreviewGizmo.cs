using PancakeDevs.ApexPhysics;
using UnityEditor;

namespace PancakeDevs.ApexPhysics.Editor
{
    internal static class ApexCrateSpawnerPreviewGizmo
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected | GizmoType.Active)]
        private static void DrawPreview(ApexCrateSpawner spawner, GizmoType gizmoType)
        {
            bool selected = (gizmoType & GizmoType.Selected) != 0;
            ApexCratePreviewRenderer.DrawScenePreview(spawner, selected);
        }
    }
}
