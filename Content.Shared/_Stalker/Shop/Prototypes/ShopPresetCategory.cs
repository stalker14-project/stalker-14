using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Shared._Stalker.Shop.Prototypes;

[DataDefinition]
public sealed partial class ShopPresetCategory
{
    [DataField("id")]
    public string Id { get; set; } = default!;

    [DataField("name")]
    public string Name { get; set; } = string.Empty;

    [DataField("priority")]
    public int Priority { get; set; }

    [DataField("items")]
    public Dictionary<string, int> Items { get; set; } = new();
}
