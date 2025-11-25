using Robust.Shared.Prototypes;
using Content.Shared.Crafting.Prototypes;

namespace Content.Shared._Stalker.Crafting.Components
{
    [RegisterComponent]
    public sealed partial class STBlueprintComponent : Component
    {
        [DataField("blueprint")]
        public ProtoId<CraftingPrototype>? BlueprintId = null;
    }
}
