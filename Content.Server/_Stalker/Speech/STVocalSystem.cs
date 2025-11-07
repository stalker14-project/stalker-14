using Content.Server.Chat.Systems;
using Content.Shared._Stalker.Speech;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Humanoid;
using Robust.Shared.Prototypes;

namespace Content.Server._Stalker.Speech;

public sealed class STVocalSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ChatSystem _chat = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STVocalComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<STVocalComponent, SexChangedEvent>(OnSexChanged);
        SubscribeLocalEvent<STVocalComponent, EmoteEvent>(OnEmote);
    }

    private void OnMapInit(Entity<STVocalComponent> entity, ref MapInitEvent args)
    {
        LoadSounds(entity);
    }

    private void OnSexChanged(Entity<STVocalComponent> entity, ref SexChangedEvent args)
    {
        LoadSounds(entity);
    }

    private void OnEmote(Entity<STVocalComponent> entity, ref EmoteEvent args)
    {
        if (args.Handled || !args.Emote.Category.HasFlag(EmoteCategory.Vocal))
            return;

        if (entity.Comp.EmoteSounds is not { } sounds)
            return;

        args.Handled = _chat.TryPlayEmoteSound(entity, _proto.Index(sounds), args.Emote);
    }

    private void LoadSounds(Entity<STVocalComponent> entity, Sex? sex = null)
    {
        sex ??= CompOrNull<HumanoidAppearanceComponent>(entity.Owner)?.Sex ?? Sex.Unsexed;

        if (!entity.Comp.Sounds.TryGetValue(sex.Value, out var protoId))
            return;

        if (!_proto.HasIndex(protoId))
            return;

        entity.Comp.EmoteSounds = protoId;
    }
}
