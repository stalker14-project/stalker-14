using Content.Client._Stalker.StalkerUi.Widgets;
using Content.Client.Gameplay;
using Content.Shared._Stalker.Psyonics;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controllers;

namespace Content.Client._Stalker.StalkerUi;

public sealed class PsyUiController : UIController, IOnStateEntered<GameplayState>, IOnStateExited<GameplayState>
{
    [Dependency] private readonly IPlayerManager _player = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<PsyEnergyChangedMessage>(OnPsyCounterUpdate);
    }

    private void OnPsyCounterUpdate(PsyEnergyChangedMessage msg, EntitySessionEventArgs args = default!)
    {
        if (_player.LocalSession?.AttachedEntity is null)
            return;

        if (UIManager.ActiveScreen?.GetWidget<PsyGui>() is { } psy)
            psy.UpdatePanelEntity(msg.NewPsy, msg.MaxPsy);
    }

    public void OnStateEntered(GameplayState state)
    {
        if (UIManager.ActiveScreen?.GetWidget<PsyGui>() is { } psy)
            psy.UpdatePanelEntity(0, 0);
    }

    public void OnStateExited(GameplayState state)
    {
        // ...
    }
}
