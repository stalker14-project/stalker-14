using Content.Server.MemoryVaccine.Components;
using Content.Server.Popups;
using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Server.Audio;
using Content.Shared.Popups;
using Content.Shared.Bed.Sleep;
using Content.Server._Stalker.MemoryLost;
using Content.Shared.MemoryVaccine;
using Content.Server.Forensics;
using Content.Shared.Interaction;

namespace Content.Server.MemoryVaccine
{
    public sealed partial class MemoryVaccineSystem : EntitySystem
    {
        [Dependency] private readonly PopupSystem _popupSystem = default!;
        [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
        [Dependency] internal readonly IEntityManager _entityManager = default!;
        [Dependency] internal readonly IMapManager _mapManager = default!;
        [Dependency] protected readonly AudioSystem Audio = default!;
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<MemoryVaccineComponent, BeforeRangedInteractEvent>(OnUseInHand);
            SubscribeLocalEvent<MemoryVaccineComponent, MemoryVaccineDoAfterEvent>(OnDoAfter);
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

        }

        public void OnUseInHand(EntityUid uid, MemoryVaccineComponent comp, BeforeRangedInteractEvent args)
        {
            if (!args.CanReach)
                return;
            OnUse(uid, comp, args.Target, args.User);
        }

        public void OnUse(EntityUid? uid, MemoryVaccineComponent comp, EntityUid? target, EntityUid user)
        {
            if (target == null)
                return;
            if (TryComp<DnaComponent>(target, out var dna) && dna != null)
            {
                var doAfterArgs = new DoAfterArgs(_entityManager, user, comp.UseTime, new MemoryVaccineDoAfterEvent(), uid, target: target, used: uid)
                {
                    BreakOnDamage = true,
                    NeedHand = true,
                    DistanceThreshold = 2f,
                };
                _popupSystem.PopupEntity("Ввод ампулы", target.Value, PopupType.LargeCaution);
                _doAfterSystem.TryStartDoAfter(doAfterArgs);
            }

        }

        public void OnDoAfter(EntityUid uid, MemoryVaccineComponent comp, MemoryVaccineDoAfterEvent args)
        {

            if (args.Handled || args.Cancelled || args.Args.Target == null)
                return;
            var target = args.Args.Target.Value;

            _popupSystem.PopupEntity("Ампула введена", uid, PopupType.LargeCaution);
            AddComp<MemoryLostComponent>(target);
            AddComp<SleepingComponent>(target);

            args.Handled = true;
            _entityManager.DeleteEntity(uid);

        }

    }
}
