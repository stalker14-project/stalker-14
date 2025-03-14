using Content.Server._Stalker.AdvancedSpawner;

namespace Content.Server._Stalker.TrashDetector;

[RegisterComponent]
public sealed partial class TrashDetectorComponent : Component
{
    [DataField] public float SearchTime { get; set; } = 5f;

    [DataField] public Dictionary<string, int> WeightModifiers { get; set; } = new();

    [DataField] public Dictionary<string, List<SpawnEntry>> ExtraPrototypes { get; set; } = new();
}
