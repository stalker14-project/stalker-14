using Content.Server.Popups;
using Content.Shared._Stalker.Characteristics;
using Content.Shared.ActionBlocker;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Verbs;

namespace Content.Server._Stalker.Characteristics.Training
{
    public sealed class CharacteristicTrainingSystem : EntitySystem
    {
        [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
        [Dependency] private readonly PopupSystem _popup = default!;
        [Dependency] private readonly ActionBlockerSystem _actionBlockerSystem = default!;
        [Dependency] private readonly CharacteristicContainerSystem _characteristicSystem = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<CharacteristicTrainingComponent, GetVerbsEvent<InteractionVerb>>(AddTrainingVerb);
            SubscribeLocalEvent<CharacteristicTrainingComponent, InteractHandEvent>(OnInteract);
            SubscribeLocalEvent<CharacteristicTrainingComponent, TrainingCompleteDoAfterEvent>(OnDoAfter);
        }

        public void AddTrainingVerb(EntityUid uid, CharacteristicTrainingComponent component, GetVerbsEvent<InteractionVerb> args)
        {
            if (!args.CanAccess || !args.CanInteract || !_actionBlockerSystem.CanMove(args.User))
                return;

            if (!HasComp<CharacteristicContainerComponent>(args.User))
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
                Act = () =>
                {
                    if (CanTrain(uid, args.User, component))
                        _doAfter.TryStartDoAfter(argsDoAfter);
                },
                Text = Loc.GetString("st-comp-training-start")
            });
        }
        private void OnInteract(EntityUid uid, CharacteristicTrainingComponent component, InteractHandEvent args)
        {
            if (args.Handled)
                return;

            if (args.Target is not { Valid: true } target || !HasComp<CharacteristicTrainingComponent>(target))
                return;

            if (!CanTrain(uid, args.User, component))
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
            var entity = (user, trainee);
            if (!_characteristicSystem.TryGetCharacteristic(entity, component.Characteristic, out var characteristic) && characteristic == null)
                return;

            var increase = characteristic.Value.Level + component.Increase;

            _characteristicSystem.TrySetCharacteristic(entity, component.Characteristic, increase, DateTime.UtcNow);

            args.Handled = true;
        }

        private bool CanTrain(EntityUid uid, EntityUid user, CharacteristicTrainingComponent component)
        {
            if (!TryComp(user, out CharacteristicContainerComponent? trainee))
                return false;

            var entity = (user, trainee);
            if (!_characteristicSystem.TryGetCharacteristic(entity, component.Characteristic, out var characteristic) && characteristic == null)
                return false;

            var canTrain = _characteristicSystem.IsTrainTimeConditionMet(entity, component.Characteristic).GetAwaiter().GetResult();
            if (!canTrain)
            {
                _popup.PopupEntity(Loc.GetString("st-already-trained-today"), uid);
                return false;
            }

            var currentValue = characteristic.Value.Level;

            // Component may have constrains on what values it can handle
            if (currentValue < component.MinValue)
            {
                _popup.PopupEntity(Loc.GetString("st-equipment-too-hard"), uid);
                return false;
            }

            if (currentValue >= component.MaxValue)
            {
                _popup.PopupEntity(Loc.GetString("st-equipment-too-easy"), uid);
                return false;
            }

            return true;
        }
    }
}
