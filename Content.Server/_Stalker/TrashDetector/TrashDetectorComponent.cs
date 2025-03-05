using Content.Server._Stalker.AdvancedSpawner;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;
using System.Collections.Generic;

namespace Content.Server.TrashDetector.Components;

[RegisterComponent]
public sealed partial class TrashDetectorComponent : Component
{

    [DataField] public float SearchTime { get; set; } = 5f;


    [DataField] public string LootSpawner { get; set; } = "RandomTrashDetectorSpawner";


    [DataField] public Dictionary<string, int> WeightModifiers { get; set; } = new();


    [DataField] public Dictionary<string, List<SpawnEntry>> ExtraPrototypes { get; set; } = new();


    [DataField] public List<string> AllowedDetectors { get; set; } = new();
}
