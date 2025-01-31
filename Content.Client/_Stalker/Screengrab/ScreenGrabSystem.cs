using Robust.Client.Graphics;
using Content.Shared._Stalker.ScreenGrabEvent;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.IO;


public sealed class ScreenGrabSystem : EntitySystem
{

    [Dependency] private readonly IClyde _clyde = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<ScreengrabRequestEvent>(OnScreenGrabEvent);
    }

    private async void OnScreenGrabEvent(ScreengrabRequestEvent e)
    {
        var image = await _clyde.ScreenshotAsync(ScreenshotType.Final);
        var array = ImageToByteArray(image);

        if (array.Length > 2_500_000)
            return;

        var msg = new ScreengrabResponseEvent { Screengrab = array };
        RaiseNetworkEvent(msg);
    }

    private byte[] ImageToByteArray(Image<Rgb24> image)
    {
        using var stream = new MemoryStream();
        image.SaveAsJpeg(stream);
        return stream.ToArray();
    }
}
