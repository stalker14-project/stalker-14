using Content.Shared._Stalker.Shop.Prototypes;
using Content.Shared.Armor;
using Content.Shared.Clothing.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Reflect;
using Robust.Shared.Prototypes;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Content.Server._Stalker.BalanceHelper;

public sealed partial class BalanceHelperSystem : EntitySystem
{
    // Находит русскую или английскую Т с цифрой. Это и будет тир
    private static readonly Regex TIER_REGEX = new Regex(@"\b[TТ]\d\b", RegexOptions.Compiled);

    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly SharedArmorSystem _armorSystem = default!;

    private ISawmill _sawmill = default!;
    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _logManager.GetSawmill("balance");
        InitializeCommands();
    }

    public string PrintAllArmor()
    {
        var sb = new StringBuilder();
        var header = $"id;name;suffix;parent;tier;noSpawn;{GetArmorHeader()}";
        sb.AppendLine(header);
        _sawmill.Info(header);
        foreach (var entProto in _proto.EnumeratePrototypes<EntityPrototype>())
        {
            if (!entProto.TryGetComponent<ArmorComponent>(out var armor))
                continue;
            _armorSystem.OnArmorInit(default, armor);
            if (!entProto.TryGetComponent<ClothingComponent>(out var clothing))
                continue;
            // Skipping artifacts which also has armor
            if ((clothing.Slots & SlotFlags.ARTIFACT) == SlotFlags.ARTIFACT)
                continue;
            // filter out zombie cloths
            if (entProto.TryGetComponent<UnremoveableComponent>(out var _))
                continue;

            entProto.TryGetComponent<ReflectComponent>(out var reflect);
            entProto.TryGetComponent<ToggleableClothingComponent>(out var togglableClothing);

            string suffix = entProto.EditorSuffix?.ToUpper() ?? string.Empty;
            // The only questionable, but reliable way to distinguish stalker and non stalker stuff
            if (!suffix.Contains("ST") && !suffix.Contains("STALKER"))
                continue;
            var parent = entProto.Parents?.Length == 1 ? entProto.Parents[0] : String.Join(", ", entProto.Parents ?? []);
            string tier = ExtractTier(suffix);

            bool noSpawn = entProto.HideSpawnMenu;
            var line = $"{entProto.ID};{entProto.Name};{suffix};{parent};{tier};{noSpawn};{GetArmorLine(armor, clothing, reflect, togglableClothing)}";
            _sawmill.Info(line);
            sb.AppendLine(line);
        }

        return sb.ToString();
    }

    public string PrintShopArmor()
    {
        var sb = new StringBuilder();
        var shops = _proto.EnumeratePrototypes<ShopPresetPrototype>();
        var header = $"shop;id;name;price;suffix;parent;{GetArmorHeader()}";
        sb.AppendLine(header);
        _sawmill.Info(header);

        foreach (var shop in shops)
        {
            var itemsForSale = shop.Categories.SelectMany(category => category.Items);

            foreach (var item in itemsForSale)
            {
                if (!_proto.TryIndex(item.Key, out var itemProto))
                    continue;
                if (!itemProto.TryGetComponent<ClothingComponent>(out var clothing))
                    continue;
                if (itemProto.TryGetComponent<ArmorComponent>(out var armor) && armor.ArmorClass.HasValue && armor.ArmorClass != 0)
                {
                    var parent = itemProto.Parents?.Length == 1 ? itemProto.Parents[0] : String.Join(", ", itemProto.Parents ?? []);
                    string suffix = itemProto.EditorSuffix?.ToUpper() ?? string.Empty;
                    var line = $"{shop.ID};{item.Key};{itemProto.Name};{item.Value};{suffix};{parent};{GetArmorLine(armor, clothing)}";
                    _sawmill.Info(line);
                    sb.AppendLine(line);
                }
            }
        }

        return sb.ToString();
    }

    public string PrintAllGuns()
    {
        var sb = new StringBuilder();
        var header = $"id;name;Cartridge;{GetGunHeader()}";
        sb.AppendLine(header);
        _sawmill.Info(header);
        foreach (var entProto in _proto.EnumeratePrototypes<EntityPrototype>())
        {
            if (entProto.Abstract)
                continue;

            if (!entProto.TryGetComponent<GunComponent>(out var gun))
                continue;

            string suffix = entProto.EditorSuffix?.ToUpper() ?? string.Empty;
            // The only questionable, but reliable way to distinguish stalker and non stalker stuff
            if (!suffix.Contains("ST") && !suffix.Contains("STALKER"))
                continue;

            string cartridge = "none";

            if (entProto.TryGetComponent<ItemSlotsComponent>(out var itemSlots))
            {
                cartridge = GetCartridgeLine(itemSlots);
            }

            Angle minAngleModifier = new();
            Angle maxAngleModifier = new();

            if (entProto.TryGetComponent<GunWieldBonusComponent>(out var wieldBonus))
            {
                minAngleModifier = wieldBonus.MinAngle;
                maxAngleModifier = wieldBonus.MaxAngle;
            }

            var line = $"{entProto.ID};{entProto.Name};{cartridge};{GetGunLine(gun, minAngleModifier, maxAngleModifier)}";
            _sawmill.Info(line);
            sb.AppendLine(line);
        }

        return sb.ToString();
    }

    public string PrintShopGuns()
    {
        var sb = new StringBuilder();
        var shops = _proto.EnumeratePrototypes<ShopPresetPrototype>();
        var header = $"shop;id;name;price;Cartridge;{GetGunHeader()}";
        sb.AppendLine(header);
        _sawmill.Info(header);

        foreach (var shop in shops)
        {
            var itemsForSale = shop.Categories.SelectMany(category => category.Items);

            foreach (var item in itemsForSale)
            {
                var itemProto = _proto.Index(item.Key);
                if (itemProto.TryGetComponent<GunComponent>(out var gun) && gun != null)
                {
                    string cartridge = "none";

                    if (itemProto.TryGetComponent<ItemSlotsComponent>(out var itemSlots))
                    {
                        cartridge = GetCartridgeLine(itemSlots);
                    }

                    Angle minAngleModifier = new();
                    Angle maxAngleModifier = new();

                    if (itemProto.TryGetComponent<GunWieldBonusComponent>(out var wieldBonus))
                    {
                        minAngleModifier = wieldBonus.MinAngle;
                        maxAngleModifier = wieldBonus.MaxAngle;
                    }

                    var line = $"{shop.ID};{item.Key};{itemProto.Name};{item.Value};{cartridge};{GetGunLine(gun, minAngleModifier, maxAngleModifier)}";
                    _sawmill.Info(line);
                    sb.AppendLine(line);
                }
            }
        }

        return sb.ToString();
    }

    private string GetCartridgeLine(ItemSlotsComponent? itemSlots)
    {
        string cartridge = "none";
        if (itemSlots != null && itemSlots.Slots != null)
        {
            var chamber = itemSlots.Slots.GetValueOrDefault("gun_chamber");
            if (chamber != null)
            {
                cartridge = chamber.Whitelist?.Tags?.FirstOrDefault() ?? cartridge;
            }
        }
        return cartridge;
    }

    private string GetGunLine(GunComponent gun, Angle minAngleModifier, Angle maxAngleModifier)
    {
        var projectileSpeed = gun.ProjectileSpeed;
        var minAngle = Math.Round(gun.MinAngle.Degrees);
        var maxAngle = Math.Round(gun.MaxAngle.Degrees);
        var minAngleDual = Math.Round((gun.MinAngle + minAngleModifier).Degrees);
        var maxAngleDual = Math.Round((gun.MaxAngle + maxAngleModifier).Degrees);
        var angleIncrease = Math.Round(gun.AngleIncrease.Degrees);
        var angleDecay = Math.Round(gun.AngleDecay.Degrees);
        var fireRate = gun.FireRate;

        return $"{projectileSpeed};{minAngle};{maxAngle};{minAngleDual};{maxAngleDual};{angleIncrease};{angleDecay};{fireRate};";
    }

    private string GetArmorLine(ArmorComponent armor, ClothingComponent clothing, ReflectComponent? reflect = null, ToggleableClothingComponent? togglableClothing = null)
    {
        var armorClass = armor.ArmorClass;
        var clotingSlot = SlotsToString(clothing.Slots);
        DamageModifierSet modifiers = armor.Modifiers ?? armor.BaseModifiers;

        var flatBlunt = modifiers.FlatReduction.FirstOrDefault(m => m.Key == "Blunt").Value;
        var flatSlash = modifiers.FlatReduction.FirstOrDefault(m => m.Key == "Slash").Value;
        var flatPiercing = modifiers.FlatReduction.FirstOrDefault(m => m.Key == "Piercing").Value;
        var flatHeat = modifiers.FlatReduction.FirstOrDefault(m => m.Key == "Heat").Value;
        var flatRadiation = modifiers.FlatReduction.FirstOrDefault(m => m.Key == "Radiation").Value;
        var flatCaustic = modifiers.FlatReduction.FirstOrDefault(m => m.Key == "Caustic").Value;
        var flatShock = modifiers.FlatReduction.FirstOrDefault(m => m.Key == "Shock").Value;
        var flatPsy = modifiers.FlatReduction.FirstOrDefault(m => m.Key == "Psy").Value;
        var flatCompression = modifiers.FlatReduction.FirstOrDefault(m => m.Key == "Compression").Value;

        float coeffBlunt = GetCoefficientOrDefault(modifiers.Coefficients, "Blunt", 1);
        float coeffSlash = GetCoefficientOrDefault(modifiers.Coefficients, "Slash", 1);
        float coeffPiercing = GetCoefficientOrDefault(modifiers.Coefficients, "Piercing", 1);
        float coeffHeat = GetCoefficientOrDefault(modifiers.Coefficients, "Heat", 1);
        float coeffRadiation = GetCoefficientOrDefault(modifiers.Coefficients, "Radiation", 1);
        float coeffCaustic = GetCoefficientOrDefault(modifiers.Coefficients, "Caustic", 1);
        float coeffShock = GetCoefficientOrDefault(modifiers.Coefficients, "Shock", 1);
        float coeffPsy = GetCoefficientOrDefault(modifiers.Coefficients, "Psy", 1); ;
        float coeffCompression = GetCoefficientOrDefault(modifiers.Coefficients, "Compression", 1);

        int pveArmorAdjust = armor.STArmorLevels?.NonPvPPhysicalAdjust ?? 0;
        int piercingAdjust = armor.STArmorLevels?.PiercingAdjust ?? 0;
        int radiationAdjust = armor.STArmorLevels?.RadiationAdjust ?? 0;
        int environmentAdjust = armor.STArmorLevels?.EnvironmentAdjust ?? 0;

        var reflectProb = reflect?.ReflectProb ?? 0;
        string togglableClothingId = togglableClothing?.ClothingPrototype.Id ?? string.Empty;

        var line = $"${clotingSlot};{armorClass};{flatBlunt};{flatSlash};{flatPiercing};{flatHeat};{flatRadiation};{flatCaustic};{flatShock};{flatPsy};{flatCompression};{coeffBlunt};{coeffSlash};{coeffPiercing};{coeffHeat};{coeffRadiation};{coeffCaustic};{coeffShock};{coeffPsy};{coeffCompression};{reflectProb};{togglableClothingId};{pveArmorAdjust};{piercingAdjust};{radiationAdjust};{environmentAdjust}";

        return line;
    }
    private string GetArmorHeader() => "slots;armorclass;FlatBlunt;FlatSlash;FlatPiercing;FlatHeat;FlatRadiation;flatCaustic;flatShock;flatPsy;flatCompression;CoeffBlunt;CoeffSlash;CoeffPiercing;CoeffHeat;CoeffRadiation;coeffCaustic;coeffShock;coeffPsy;coeffCompression;reflectProb;togglableClothingId;pveArmorAdjust;piercingAdjust;radiationAdjust;environmentAdjust";

    private string GetGunHeader() => "projectileSpeed;minAngle;maxAngle;minAngleDual;maxAngleDual;angleIncrease;angleDecay;fireRate;";
    public float GetCoefficientOrDefault(Dictionary<string, float>? coefficients, string key, int defaultValue)
    {
        if (coefficients == null)
        {
            return defaultValue;
        }
        var kvp = coefficients.FirstOrDefault(m => m.Key == key);
        return kvp.Equals(default(KeyValuePair<string, float>)) ? defaultValue : kvp.Value;
    }

    private string SlotsToString(SlotFlags slots)
    {
        if (slots == SlotFlags.NONE)
            return SlotFlags.NONE.ToString();

        var flags = Enum.GetValues(typeof(SlotFlags))
                        .Cast<SlotFlags>()
                        .Where(flag => flag != SlotFlags.NONE && slots.HasFlag(flag));

        return string.Join(", ", flags);
    }
    private string ExtractTier(string input)
    {
        var match = TIER_REGEX.Match(input);
        return match.Success ? match.ToString().Substring(1) : string.Empty;
    }
}
