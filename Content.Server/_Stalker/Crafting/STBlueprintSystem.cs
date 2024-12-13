using Content.Shared._Stalker.Crafting.Components;
using Content.Shared.Examine;
using Robust.Shared.Prototypes;
using System.Linq;
using System.Text;
using Content.Shared.Tag;
using Content.Shared.Crafting.Prototypes;
using System.Diagnostics;

namespace Content.Server.Crafting;
/// <summary>
/// Система рецептів. Її фішка в тому, що предмети-рецепти, створені з її допомогою, завжди матимуть коретні
/// інгредієнти, тому що вона братиме дані безпосередньо з прототипу-рецепту(craftRecipe).
/// </summary>
public sealed class STBlueprintSystem : EntitySystem
{
    [Dependency] private readonly MetaDataSystem _metaSystem = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly TagSystem _tagSystem = default!;
    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly IComponentFactory _componentFactory = default!;

    private ISawmill _sawmill = default!;
    private Dictionary<string, string> _descriptionsByBlueprint = new();
    private Dictionary<string, string> _namesByBlueprint = new();
    private const string WORKBENCH_TAG = "STWorkbench";
    private Dictionary<string, string> _workbenchNamesById = default!;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _logManager.GetSawmill("Blueprint");
        _workbenchNamesById = _proto.EnumeratePrototypes<EntityPrototype>().Where(entity =>
            entity.TryGetComponent<TagComponent>(out var tag, _componentFactory) && _tagSystem.HasTag(tag, WORKBENCH_TAG)
        ).ToDictionary(entity => entity.ID, entity => entity.Name);

        if (_workbenchNamesById.Count == 0)
        {
            _sawmill.Error($"Немає діючих верстаків. Перевірте наявність {WORKBENCH_TAG}");
        }
        AddDescriptions();
        SubscribeLocalEvent<STBlueprintComponent, ExaminedEvent>(OnBlueprintExamine);
        SubscribeLocalEvent<STBlueprintComponent, ComponentStartup>(OnComponentStartup);
    }

    /// <summary>
    /// При Shift-Right click показує детальний рецепт крафта в описі
    /// </summary>
    public void OnBlueprintExamine(EntityUid uid, STBlueprintComponent component, ExaminedEvent args)
    {
        if (!component.BlueprintId.HasValue)
            return;
        if (!args.IsInDetailsRange)
            return;
        if (!_descriptionsByBlueprint.TryGetValue(component.BlueprintId.Value.Id, out var description))
            return;

        args.PushMarkup(description);
    }
    /// <summary>
    /// Для того щоб поміняти ім'я у компонента автоматично, щоб рецепти не застарівали. На жаль,
    /// якщо рецепт розташований в інтерфейсі торговця, він показуватиме ім'я з yml. Але хоча б якщо на підлозі
    /// і т.д., ми отримаємо актуальне ім'я
    /// </summary>
    public void OnComponentStartup(EntityUid uid, STBlueprintComponent component, ComponentStartup args)
    {
        if (!component.BlueprintId.HasValue)
            return;

        if (!_namesByBlueprint.TryGetValue(component.BlueprintId.Value.Id, out var name))
            return;

        _metaSystem.SetEntityName(uid, name);
    }

    private void AddDescriptions()
    {
        var blueprints = _proto.EnumeratePrototypes<CraftingPrototype>().ToList();

        foreach (var blueprint in blueprints)
        {
            var stringBuilder = new StringBuilder();
            string workbench = Loc.GetString("st-blueprint-anyworkbench");
            if (blueprint.RequiredWorkbench != null && _workbenchNamesById.TryGetValue(blueprint.RequiredWorkbench, out var workbenchName))
            {
                workbench = workbenchName;
            }

            string workbenchDetails = $"{Loc.GetString("st-blueprint-workbench")}: {workbench}";
            stringBuilder.AppendLine(workbenchDetails);

            stringBuilder.AppendLine(Loc.GetString("st-blueprint-ingridients"));
            foreach (var (id, details) in blueprint.Items)
            {
                if (!_proto.TryIndex(id, out var prototype))
                {
                    _sawmill.Error($"Існує рецепт {blueprint.ID} з інгредієнтом {id}. Але прототип інгредієнта відсутній");
                    stringBuilder.AppendLine(Loc.GetString("st-blueprint-not-found"));
                    continue;
                }
                stringBuilder.AppendLine($"\t{prototype.Name} {details.Amount} {GetCatalistIcon(details.Catalyzer)}");
            }
            stringBuilder.AppendLine(Loc.GetString("st-blueprint-result"));
            string? resultName = null;

            foreach (var id in blueprint.ResultProtos)
            {
                if (!_proto.TryIndex(id, out var prototype))
                {
                    _sawmill.Error($"Існує рецепт {blueprint.ID} з результатом {id}. Але прототип результату відсутній");
                    stringBuilder.AppendLine(Loc.GetString("st-blueprint-not-found"));
                    continue;
                }
                if (resultName == null)
                    resultName = prototype.Name;
                stringBuilder.AppendLine($"\t{prototype.Name}");
            }
            var description = stringBuilder.ToString();
            var multipleResults = blueprint.ResultProtos.Count > 1 ? Loc.GetString("st-blueprint-multiple-results") : string.Empty;
            resultName = $"{Loc.GetString("st-blueprint-prefix")} {resultName} {multipleResults}";

            _namesByBlueprint.Add(blueprint.ID, resultName);
            _descriptionsByBlueprint.Add(blueprint.ID, description);
        }
    }

    private string GetCatalistIcon(bool isCatalyzer)
    {
        return isCatalyzer ? Loc.GetString("st-blueprint-ingridient-saved") : "";
    }
}
