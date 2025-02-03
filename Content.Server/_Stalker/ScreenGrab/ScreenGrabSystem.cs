using Content.Shared._Stalker.ScreenGrabEvent;
using Robust.Shared.Player;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Png;
using System.IO;
using Robust.Shared.Network;

namespace Content.Server._Stalker.ScreenGrab;

public sealed class ScreengrabSystem : EntitySystem
{
    private readonly Dictionary<NetUserId, Guid> _pendingRequests = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<ScreengrabResponseEvent>(OnScreengrabReply);
    }

    public void SendScreengrabRequest(ICommonSession session)
    {
        var token = Guid.NewGuid();
        _pendingRequests[session.UserId] = token;

        RaiseNetworkEvent(new ScreengrabRequestEvent { Token = token }, session);
    }

    private void OnScreengrabReply(ScreengrabResponseEvent ev, EntitySessionEventArgs args)
    {
        if (!_pendingRequests.TryGetValue(args.SenderSession.UserId, out var expectedToken) || ev.Token != expectedToken)
        {
            Log.Warning($"screengrab failed checks {args.SenderSession.Name}.");
            return;
        }

        _pendingRequests.Remove(args.SenderSession.UserId);

        if (ev.Screengrab.Length == 0)
            return;

        using var image = Image.Load<Rgb24>(ev.Screengrab);

        string saveFolder = Path.Combine(Directory.GetCurrentDirectory(), "Data", "Screenshots");
        Directory.CreateDirectory(saveFolder);

        string fileName = $"{args.SenderSession.Name}_{DateTime.UtcNow:yyyy-MM-dd_HH-mm-ss}.png";
        string fullPath = Path.Combine(saveFolder, fileName);

        image.Save(fullPath, new PngEncoder());

        Log.Info($"Скриншот от {args.SenderSession.Name} сохранён: {fullPath}");
    }
}
