using Content.Shared.Whitelist;

namespace Content.Shared._Stalker.ZoneAnomaly.Triggers;

public abstract partial class ZoneAnomalyTriggerCollideComponent : Component
{
    /// <summary>
    /// I don't hate working with fucking masks, fucking bullets go to hell.
    /// </summary>
    public readonly EntityWhitelist? BaseBlacklist = new()
    {
        Components = new []
        {
            "Projectile",
            "FishingLure",
        },
    };

    [DataField]
    public EntityWhitelist? Whitelist;

    [DataField]
    public EntityWhitelist? Blacklist;
}
