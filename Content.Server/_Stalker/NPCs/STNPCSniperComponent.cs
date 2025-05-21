using Content.Shared.Damage;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;

namespace Content.Server._Stalker.NPCs;

[RegisterComponent]
public sealed partial class STNPCSniperComponent : Component
{
    [DataField]
    public int Range = 10;

    [DataField]
    public SoundSpecifier? SoundGunshot;

    [DataField]
    public DamageSpecifier? Damage;

    [DataField]
    public HashSet<LocId> MessageShoot = new();

    /// <summary>
    /// should system read NPCFactionMember comp
    /// </summary>
    [DataField]
    public bool Reader = false;

    /// <summary>
    /// the target faction which will be ignored as friendly
    /// </summary>
    [DataField]
    public string Faction;
}
