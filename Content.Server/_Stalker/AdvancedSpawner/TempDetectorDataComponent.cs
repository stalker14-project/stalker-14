using Content.Server._Stalker.TrashDetector;

namespace Content.Server._Stalker.AdvancedSpawner;

[RegisterComponent]
public sealed partial class TempDetectorDataComponent : Component
{
    public TrashDetectorComponent Detector { get; set; }


    [DataField] public Dictionary<string, int> WeightModifiers = new();


    [DataField] public Dictionary<string, List<SpawnEntry>> ExtraPrototypes = new();
}
