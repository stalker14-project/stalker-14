using Content.Shared._Stalker.RadioStalker.Components;
using Content.Shared.Radio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stalker.RadioStalker.EntitySystems;

public sealed partial class ConfigurableEncryptionKeySystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    private readonly Dictionary<int, RadioChannelPrototype> _keyFreq = new();
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ConfigurableEncryptionKeyComponent, SelectEncryptionKeyMessage>(OnChannelEntered);
    }

    private void OnChannelEntered(EntityUid uid, ConfigurableEncryptionKeyComponent component, SelectEncryptionKeyMessage args)
    {
        foreach (var proto in _protoMan.EnumeratePrototypes<RadioChannelPrototype>())
        {
            if (!_keyFreq.ContainsKey(proto.Frequency))
            {
                _keyFreq.TryAdd(proto.Frequency, proto);
            }

            foreach (var channel in _keyFreq)
            {
                if (channel.Key.ToString() == args.Channel)
                {
                    component.Channel = channel.Value.ID;
                }
            }
        }
    }
}
