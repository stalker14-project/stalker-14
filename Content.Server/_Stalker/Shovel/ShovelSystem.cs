using Content.Shared._Stalker.Lopata;
using Content.Shared.DoAfter;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._Stalker.Shovel;

public sealed class ShovelSystem : EntitySystem
{

    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefinitionManager = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private readonly HashSet<string> _tileForPlowGround = new(0);
    private readonly HashSet<string> _entitiesForPlowGround = new(0);

    private List<SoundSpecifier> _shovelSounds = new(0);

    public override void Initialize()
    {
        SubscribeLocalEvent<StalkerShovelComponent, GetVerbsEvent<ActivationVerb>>(ShovelMenu);
        SubscribeLocalEvent<StalkerShovelComponent, PlowGroundDoAfterEvent>(OnDoAfterPlowGround);
        _tileForPlowGround.Add("FloorPlanetGreenGrass");
        _tileForPlowGround.Add("FloorPlanetYellowGrass");
        for (var i = 0; i <= 6; i++)
        {
            _shovelSounds.Add(new SoundPathSpecifier("/Audio/_Stalker/Effects/LopataSound/Lopata"+i+".ogg"));
        }
        _entitiesForPlowGround.Add("hydroponicsSoil");
        _entitiesForPlowGround.Add("SoilStalker");
        _entitiesForPlowGround.Add("CrateStoneGrave");
    }

    private SoundSpecifier GetRandomShovelSound()
    {
        return _shovelSounds[_random.Next(0, _shovelSounds.Count)];
    }

    private void OnDoAfterPlowGround(EntityUid uid, StalkerShovelComponent component, PlowGroundDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        var shovelUserPositionX = 0;
        var shovelUserPositionY = 0;
        if (TryComp<TransformComponent>(args.User, out var transformComponent))
        {
            var shovelUserPosition = transformComponent.LocalPosition;
            shovelUserPositionX = (int)shovelUserPosition.X;
            shovelUserPositionY = (int)shovelUserPosition.Y;
        }

        var entities = _entityManager.GetEntities();
        var foundInPosition = false;

        foreach (var entity in entities)
        {
            if (_entitiesForPlowGround.Contains(GetPrototypeName(entity)))
            {
                if ((int)Transform(entity).Coordinates.X==shovelUserPositionX)
                {
                    if ((int)Transform(entity).Coordinates.Y==shovelUserPositionY)
                    {
                        foundInPosition = true;
                        break;
                    }
                }
            }
            if (foundInPosition)
            {
                break;
            }
        }
        if (foundInPosition == false)
        {
            var parent = args.User;
            if (_entityManager.TryGetComponent(parent, out TransformComponent? xform))
            {
                parent = xform.ParentUid;
            }

            if (xform!=null)
            {
                _entityManager.SpawnEntity(args.NameProrotype, new EntityCoordinates(parent,xform.Coordinates.X,xform.Coordinates.Y));
            }
        }
        else
        {
            _popup.PopupEntity("Місце вже зайнято", args.User, PopupType.Large);
        }
    }

    private (int X,int Y)? PosInt(EntityUid inputEntity)
    {
        (int X, int Y)? returnPos=null;

        if (_entityManager.TryGetComponent(inputEntity, out TransformComponent? xform))
        {
            returnPos = new ValueTuple<int, int>((int) xform.LocalPosition.X, (int) xform.LocalPosition.Y);
        }

        return returnPos;
    }

    private string GetPrototypeName(EntityUid inputItemUid)
    {
        if (!TryComp(inputItemUid, out MetaDataComponent? metaData))
            return string.Empty;

        return metaData.EntityPrototype?.ID == null ? string.Empty : metaData.EntityPrototype.ID;
    }

    private string GetTileUnderEntityPlayer(EntityUid playerUid)
    {
        if (!_entityManager.TryGetComponent(playerUid, out TransformComponent? xform))
            return string.Empty;

        if (!_entityManager.TryGetComponent(xform.GridUid, out MapGridComponent? grid))
            return string.Empty;

        var posPlayer = PosInt(playerUid);

        TileRef? foundTile = null;

        foreach (var tile in grid.GetAllTiles())
        {
            if (posPlayer?.X != tile.X)
                continue;

            if (posPlayer.Value.Y != tile.Y)
                continue;

            foundTile = tile;
            break;
        }

        if (foundTile == null)
            return string.Empty;

        var id = foundTile.Value.Tile.TypeId;
        var tileDef = _tileDefinitionManager[id];
        var tileDefId = tileDef.ID;
        return tileDefId;
    }

    private void ShovelMenu(Entity<StalkerShovelComponent> ent, ref GetVerbsEvent<ActivationVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var @event = args;

        ActivationVerb verb = new()
        {
            Text = Loc.GetString("Викопати грядку"),
            Icon = new SpriteSpecifier.Texture(new ("/Textures/Interface/VerbIcons/light.svg.192dpi.png")),

            Act = () => PlowGround(@event.User, ent,"hydroponicsSoil",30)
        };
        if (ent.Comp.CanPlow)
        {
            args.Verbs.Add(verb);
        }

        ActivationVerb verb2 = new()
        {
            Text = Loc.GetString("Викопати могилу"),
            Icon = new SpriteSpecifier.Texture(new ("/Textures/Interface/VerbIcons/light.svg.192dpi.png")),

            Act = () => PlowGround(@event.User, ent,"CrateStoneGrave",60)
        };
        if (ent.Comp.CanMakeGrave)
        {
            args.Verbs.Add(verb2);
        }

    }

    private void PlowGround(EntityUid eventUser, Entity<StalkerShovelComponent> ent, string prototypeName,float time)
    {
        if (!ent.Comp.IsFree)
            return;

        var tileNameId = GetTileUnderEntityPlayer(eventUser);
        if (_tileForPlowGround.Contains(tileNameId))
        {
            _audio.PlayPvs(GetRandomShovelSound(), eventUser);

            var doAfterEventArgs = new DoAfterArgs(EntityManager, eventUser, time,
                new PlowGroundDoAfterEvent(prototypeName)
                , ent, target: eventUser, used: ent)
            {
                BreakOnDamage = true,
                NeedHand = true,
            };

            _doAfterSystem.TryStartDoAfter(doAfterEventArgs);
        }
        else
        {
            _popup.PopupEntity("Не відповідна місцевість", eventUser, PopupType.Large);
        }
    }
}
