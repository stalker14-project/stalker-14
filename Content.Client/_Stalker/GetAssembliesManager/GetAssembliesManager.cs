using System.Linq;
using Content.Shared._Stalker.GetAssembliesLogger;



namespace Content.Client._Stalker.GetAssembliesManager;
public sealed class GetAssembliesManager : EntitySystem
{
    public override void Initialize()
    {
        CheckForLib();
    }

    public static void CheckForLib()
    {
        try
        {
            //var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();

            //var harmonyType = loadedAssemblies
            //    .SelectMany(a =>
            //    {
            //        try
            //        {
            //            return a.GetTypes();
            //        }
            //        catch
            //        {
            //            return Array.Empty<Type>();
            //        }
            //    })
            //    .FirstOrDefault(t => t.FullName == "HarmonyLib.Harmony");

            //if (harmonyType != null)
            //{
            //    var harmonyAssembly = harmonyType.Assembly;
            //    var ev = new GetAssembliesLoggerEvent($"Harmony Lib Found: {harmonyAssembly.FullName}");
            //    IoCManager.Resolve<IEntityManager>().EventBus.RaiseEvent(EventSource.Local, ev);
            //}
        }
        catch (Exception ex){}
    }

}

