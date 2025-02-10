using Robust.Shared.Configuration;

namespace Content.Shared._Stalker.StalkerCCVars;

[CVarDefs]
public sealed class StalkerCCVars
{
    /**
     * Tape Player
     */

    /// <summary>
    /// Параметр отключения школьников с колонками у клиента.
    /// </summary>
    public static readonly CVarDef<bool> TapePlayerClientEnabled =
        CVarDef.Create("tape_player.client_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);
}
