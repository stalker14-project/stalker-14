using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Content.Shared.Store;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Stalker.Shop.Prototypes;

[Prototype("barterPreset")]
public sealed class BarterPresetPrototype : IPrototype
{
    [IdDataField] public string ID { get; } = default!;

    [DataField("requirements")]
    public Dictionary<string, int> ItemRequirements = new();
}
