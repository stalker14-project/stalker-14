using Content.Server.TrashDetector.Components;

namespace Content.Server._Stalker.AdvancedSpawner
{
    [RegisterComponent]
    public sealed partial class TempDetectorDataComponent : Component
    {
        public TrashDetectorComponent Detector { get; set; }
    }
}
