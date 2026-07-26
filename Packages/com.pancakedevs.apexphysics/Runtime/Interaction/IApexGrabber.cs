using UnityEngine;

namespace PancakeDevs.ApexPhysics
{
    /// <summary>
    /// Small integration contract for anything capable of holding an ApexGrabbable.
    /// XR, desktop, NPC, and networking modules can implement the same interface.
    /// </summary>
    public interface IApexGrabber
    {
        int GrabberId { get; }
        Transform GripTarget { get; }
        ApexHandedness Handedness { get; }
        bool IsHolding { get; }

        void ForceRelease();
    }
}
