using Content.Server.TrashDetector.Components;
using System.Collections.Generic;

namespace Content.Server._Stalker.AdvancedSpawner
{
    [RegisterComponent]
    public sealed partial class TempDetectorDataComponent : Component
    {
        public TrashDetectorComponent Detector { get; set; }


        [DataField] public Dictionary<string, int> WeightModifiers = new();


        [DataField] public Dictionary<string, List<SpawnEntry>> ExtraPrototypes = new();
    }
}
