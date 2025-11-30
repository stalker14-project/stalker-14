using System.Numerics;
using Content.Server.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Players;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Content.Server._Stalker.Other;
using Content.Server._Stalker.Procedural;
using Content.Shared.Administration;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Decals;
using Content.Shared.FixedPoint;
using Content.Shared.Ghost;
using Content.Shared.Roles.Jobs;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Administration.Commands;

[AdminCommand(AdminFlags.Spawn)]
public sealed class StalkerDecal : IConsoleCommand
{
    public string Command => "spawndecal";
    public string Description => "Spawn decals";
    public string Help => "Usage: spawndecal";

    [Dependency] private readonly IConsoleHost _console = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;


    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        shell.WriteLine("Try spawndecal");

        int Arg1=Convert.ToInt32(args[0]);
        int Arg2=Convert.ToInt32(args[1]);

        string[] ArrSplitted=argStr.Split('{');
        string JsonStr = "";


        for (int i = 1; i < ArrSplitted.Length; i++)
        {
            JsonStr += "{"+ArrSplitted[i];
        }

        string Arg3 = JsonStr;


        Logger.Debug("Arg1="+Arg1);
        Logger.Debug("Arg2="+Arg2);
        Logger.Debug("Arg3 json="+Arg3);

        Logger.Debug("Test ARG FULL="+argStr);


       var player = shell.Player;
        if (player== null)
        {
            return;
        }

        Logger.Debug("DecalCommandOptions");

        DecalCommandOptions OptionsCommand = new DecalCommandOptions();

        DecalCommandOptions? LoadedOptionsCommand = FromJson(Arg3);
        Logger.Debug("DecalCommandOptions OK");


        if (LoadedOptionsCommand != null)
        {
            OptionsCommand = LoadedOptionsCommand;
        }
        else
        {
            Logger.Debug("Arg3 JSON ERROR="+Arg3);
            return;
        }


        Logger.Debug("JsonTest="+ToJson(OptionsCommand));

        var playerMgr = IoCManager.Resolve<IPlayerManager>();
        var sysMan = IoCManager.Resolve<IEntitySystemManager>();
        var ticker = sysMan.GetEntitySystem<GameTicker>();
        var mind = sysMan.GetEntitySystem<SharedMindSystem>();
        var entityManager = IoCManager.Resolve<IEntityManager>();
        var _EntitySystem = sysMan.GetEntitySystem<SharedMapSystem>();
        var jobMan = sysMan.GetEntitySystem<SharedJobSystem>();
        var _damageableSystem = sysMan.GetEntitySystem<DamageableSystem>();
        var CodeSystem = sysMan.GetEntitySystem<CodeSystem>();

        if (player.AttachedEntity == null)
            return;

        if (!entityManager.TryGetComponent(player.AttachedEntity, out GhostComponent? GComp))
            return;

        shell.WriteLine("GComp="+GComp.Owner);

        var Coords = CodeSystem.CSTransform(GComp.Owner).Coordinates;

        if (!entityManager.TryGetComponent(player.AttachedEntity, out TransformComponent? TComponent))
            return;

        //var ent = entityManager.SpawnEntity("Crowbar", NewPos(TComponent,0,0));

        OptionsDecal OptionsDecal = new OptionsDecal();
        Color DecalColor = new Color(255,255,255,255);

        OptionsDecal.ImportedDecalsFromJson.AddRange(OptionsCommand.Zones);
        OptionsDecal.Install("",Arg1, Arg2);


        Logger.Debug("Anime AllCells.Count="+OptionsDecal.DecalBlueprintCells.AllCells.Count);

        foreach (var OneCell in OptionsDecal.DecalBlueprintCells.AllCells)
        {
            if (OneCell.Prototype!="EMPTY")
            {
                var OneDecal = new Decal(NewPosDecal(TComponent,OneCell.PosX+NextFloat(-OneCell.RandomOffset,OneCell.RandomOffset),OneCell.PosY+NextFloat(-OneCell.RandomOffset,OneCell.RandomOffset)), OneCell.Prototype, DecalColor, Angle.Zero, 0, true);
                CodeSystem.SpawnDecal(OneDecal,NewPos(TComponent,OneCell.PosX,OneCell.PosY));
            }
        }
    }

    float NextFloat(float min, float max){
        System.Random random = new System.Random();
        double val = (random.NextDouble() * (max - min) + min);
        return (float)val;
    }

    public EntityCoordinates NewPos(TransformComponent TComp,float X,float Y)
    {
        var entityManager = IoCManager.Resolve<IEntityManager>();
        var parent = TComp.Owner;
        if (entityManager.TryGetComponent(parent, out TransformComponent? TComponent))
        {
            parent = TComponent.ParentUid;
        }
        return new EntityCoordinates(parent,TComp.Coordinates.X+X,TComp.Coordinates.Y+Y);
    }

    public Vector2 NewPosDecal(TransformComponent TComp,float X,float Y)
    {
        var entityManager = IoCManager.Resolve<IEntityManager>();
        var parent = TComp.Owner;
        if (entityManager.TryGetComponent(parent, out TransformComponent? TComponent))
        {
            parent = TComponent.ParentUid;
        }

        return new Vector2(TComp.Coordinates.X+X,TComp.Coordinates.Y+Y);
    }

    public DecalCommandOptions? FromJson(string JsonText)
    {
        return  JsonSerializer.Deserialize<DecalCommandOptions>(JsonText);
    }

    public string ToJson(DecalCommandOptions InputDecalBlueprint)
    {
        return JsonSerializer.Serialize(InputDecalBlueprint);
    }


}

[System.Serializable]
public class DecalCommandOptions
{
    public string PresetName { get; set; } = "TestName";
    public List<ZoneDecal> Zones { get; set; } = new List<ZoneDecal>(0);

}






