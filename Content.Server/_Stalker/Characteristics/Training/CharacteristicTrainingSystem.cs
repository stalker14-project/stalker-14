using Content.Server.Popups;
using Content.Shared._Stalker.Characteristics;
using Content.Shared.ActionBlocker;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Verbs;

namespace Content.Server._Stalker.Characteristics.Training
{
    public class CharacteristicTrainingSystem : EntitySystem
    {
        [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
        [Dependency] private readonly PopupSystem _popup = default!;
        [Dependency] private readonly ActionBlockerSystem _actionBlockerSystem = default!;
        [Dependency] private readonly CharacteristicContainerSystem _characteristicSystem = default!;
        [Dependency] private readonly CharacteristicSystem _characteristic = default!;
        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<CharacteristicTrainingComponent, GetVerbsEvent<InteractionVerb>>(AddTrainingVerb);
            SubscribeLocalEvent<CharacteristicTrainingComponent, AfterInteractEvent>(OnInteract);
            SubscribeLocalEvent<CharacteristicTrainingComponent, TrainingCompleteDoAfterEvent>(OnDoAfter);
        }

        public void AddTrainingVerb(EntityUid uid, CharacteristicTrainingComponent component, GetVerbsEvent<InteractionVerb> args)
        {
            if (!args.CanAccess || !args.CanInteract || !_actionBlockerSystem.CanMove(args.User))
                return;

            if (!TryComp(args.User, out CharacteristicContainerComponent? containerComponent))
                return;

            var argsDoAfter = new DoAfterArgs(EntityManager, args.User, component.Delay, new TrainingCompleteDoAfterEvent(), uid, uid)
            {
                NeedHand = true,
                BreakOnHandChange = false,
                BreakOnMove = true,
                CancelDuplicate = true,
                BlockDuplicate = true
            };

            // TODO VERBS ICON add a climbing icon?
            args.Verbs.Add(new InteractionVerb
            {
                Act = () => _doAfter.TryStartDoAfter(argsDoAfter),
                Text = Loc.GetString("st-comp-training-start")
            });
        }
        private void OnInteract(EntityUid uid, CharacteristicTrainingComponent component, AfterInteractEvent args)
        {
            if (args.Handled)
                return;

            if (!args.CanReach || args.Target is not { Valid: true } target || !HasComp<CharacteristicTrainingComponent>(target))
                return;

            var argsDoAfter = new DoAfterArgs(EntityManager, args.User, component.Delay, new TrainingCompleteDoAfterEvent(), uid, uid)
            {
                NeedHand = true,
                BreakOnHandChange = false,
                BreakOnMove = true,
                CancelDuplicate = true,
            };
            _doAfter.TryStartDoAfter(argsDoAfter);
            args.Handled = true;
        }

        private void OnDoAfter(EntityUid uid, CharacteristicTrainingComponent component, TrainingCompleteDoAfterEvent args)
        {
            if (args.Cancelled || args.Handled || args.Args.Target == null)
                return;

            if (args.User is not { Valid: true } user)
                return;

            if (!TryComp(user, out CharacteristicContainerComponent? trainee))
                return;
            if (component is null)
                return;
            var entity = (uid, trainee);
            if (!_characteristicSystem.TryGetCharacteristic(entity, component.Characteristic, out var characteristic) && characteristic == null)
                return;
            int value = characteristic.Value.Level;

            // Component may have constrains on what values it can handle
            if (value >= component.MaxValue || value < component.MinValue)
            {
                _popup.PopupEntity(Loc.GetString("st-find-better-equipment"), uid);
                return;
            }
            int increase = value + component.Increase;

            bool canTrain = _characteristicSystem.IsTrainTimeConditionMet(entity, component.Characteristic).GetAwaiter().GetResult();
            if (!canTrain)
            {
                _popup.PopupEntity(Loc.GetString("st-already-trained-today"), uid);
                return;
            }

            _characteristicSystem.TrySetCharacteristic((uid, trainee), component.Characteristic, increase, DateTime.UtcNow);
            Dirty(uid, component);

            args.Handled = true;
        }

    }
}
