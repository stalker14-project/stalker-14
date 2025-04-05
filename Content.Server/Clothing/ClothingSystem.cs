using Content.Server.Humanoid;
using Content.Server.Preferences.Managers;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.IdentityManagement.Components;
using Content.Shared.Inventory.Events;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Preferences;
using Robust.Shared.Enums;
using Robust.Shared.Player;

namespace Content.Server.Clothing;

public sealed class ServerClothingSystem : ClothingSystem
{
    // stalker-changes-starts

    [Dependency] private readonly MetaDataSystem _metaSystem = default!;
    [Dependency] private readonly HumanoidAppearanceSystem _humanoidSystem = default!;
    [Dependency] private readonly IServerPreferencesManager _prefsManager = default!;

    protected override void OnGotEquipped(EntityUid uid, ClothingComponent component, GotEquippedEvent args)
    {
        base.OnGotEquipped(uid, component, args);
        bool isIdentityBlocker = HasComp<IdentityBlockerComponent>(uid);
        if (args.Slot == "mask" && isIdentityBlocker)
            ChangeName(args.Equipee, true);
    }

    protected override void OnGotUnequipped(EntityUid uid, ClothingComponent component, GotUnequippedEvent args)
    {
        base.OnGotUnequipped(uid, component, args);
        bool isIdentityBlocker = HasComp<IdentityBlockerComponent>(uid);
        if (args.Slot == "mask" && isIdentityBlocker)
            ChangeName(args.Equipee, false);
    }

    private void ChangeName(EntityUid entity, bool isHidden)
    {
        if (!TryComp<ActorComponent>(entity, out var actor))
            return;

        HumanoidCharacterProfile profile;

        if (_prefsManager.TryGetCachedPreferences(actor.PlayerSession.UserId, out var preferences))
        {
            profile = (HumanoidCharacterProfile)preferences.GetProfile(preferences.SelectedCharacterIndex);
        }
        else
        {
            profile = HumanoidCharacterProfile.Random();
        }

        int num = profile.Name.GetHashCode();
        byte[] bytes = BitConverter.GetBytes(num);
        string base64String = Convert.ToBase64String(bytes);
        string code = base64String.Substring(0, 3);

        if (!isHidden)
        {
            _metaSystem.SetEntityName(entity, profile.Name);
            return;
        }

        var ageString = _humanoidSystem.GetAgeRepresentation(profile.Species, profile.Age);
        var genderString = profile.Gender switch
        {
            Gender.Female => Loc.GetString("identity-gender-feminine"),
            Gender.Male => Loc.GetString("identity-gender-masculine"),
            Gender.Epicene or Gender.Neuter or _ => Loc.GetString("identity-gender-person")
        };

        _metaSystem.SetEntityName(entity, $"[${code}] {genderString} {ageString}");
    }
    // stalker-changes-ends
}
