using Robust.Shared.Configuration;

namespace Content.Shared._Stalker.CCCCVars;

// ReSharper disable once InconsistentNaming
public sealed partial class CCCCVars
{  
    /*
     * Stalker Sponsors
     */
    public static readonly CVarDef<string> SponsorsApiUrl =
        CVarDef.Create("sponsors.api_url", "http://127.0.0.1:2424/api", CVar.SERVERONLY | CVar.CONFIDENTIAL);

    public static readonly CVarDef<string> SponsorsApiKey =
        CVarDef.Create("sponsors.api_key", "key", CVar.SERVERONLY | CVar.CONFIDENTIAL);

    public static readonly CVarDef<int> PriorityJoinTier =
        CVarDef.Create("sponsors.priorityJoinTier", 2, CVar.SERVERONLY | CVar.CONFIDENTIAL);

    public static readonly CVarDef<string> SponsorsGuildId =
        CVarDef.Create("sponsors.guild_id", "1148992175347089468", CVar.SERVERONLY | CVar.CONFIDENTIAL);

}
