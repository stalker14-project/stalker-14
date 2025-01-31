using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.IO;
using SixLabors.ImageSharp.Formats.Png;
using Content.Shared._Stalker.ScreenGrabEvent;
using Robust.Shared.Player;

namespace Content.Server._Stalker.ScreenGrab;

public sealed class ScreengrabSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<ScreengrabResponseEvent>(OnScreengrabReply);

    }

    public void ScreengrabRequest(ICommonSession session)
    {
        RaiseNetworkEvent(new ScreengrabRequestEvent(), session);

    }

    public void OnScreengrabReply(ScreengrabResponseEvent ev, EntitySessionEventArgs args)
    {
        if (ev.Screengrab.Length == 0)
            return;

        var imagedata = ev.Screengrab;
        using var image = Image.Load<Rgb24>(imagedata);

        string saveFolder = Path.Combine(Directory.GetCurrentDirectory(), "Data", "Screenshots");

        Directory.CreateDirectory(saveFolder);

        string fileName = $"{args.SenderSession.Name}_{DateTime.UtcNow:yyyy-MM-dd_HH-mm-ss}.png";
        string fullPath = Path.Combine(saveFolder, fileName);

        image.Save(fullPath, new PngEncoder());

        Log.Info($"Скриншот от {args.SenderSession.Name} сохранён: {fullPath}");
    }
}
