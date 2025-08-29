using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Shared._Stalker;

public sealed class STCategoryFilterSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        // This will remove all vanilla items and all ìtems which are not 
        // inheriting STBaseEntity (spoiler, about 5% of stalker's items inheriting that
        // Don't turn it on until you fix that please)
        // _cfg.SetCVar(CVars.EntitiesCategoryFilter, "ForkFiltered");
    }
}
