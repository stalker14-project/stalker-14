using Robust.Shared.GameStates;
using Robust.Shared.Serialization;


namespace Content.Server._Stalker.ZoneAnomaly.Devices


{
    [RegisterComponent, NetworkedComponent]
    public sealed partial class ZoneAnomalyDestructorComponent : Component
    {
        [DataField]
        public float Delay = 20f;

        [DataField]
        public string TargetPrototype = "ZoneAnomalyBase"; // change to match your actual anomaly prototype ID
    }
}
