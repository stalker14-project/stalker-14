using Robust.Client.Graphics;
using Content.Shared._Stalker.ScreenGrabEvent;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.IO;
using Content.Client.Viewport;
using Robust.Client.State;
public sealed class ScreenGrabSystem : EntitySystem
{
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IStateManager _stateManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<ScreengrabRequestEvent>(OnScreenGrabEvent);
    }

    private async void OnScreenGrabEvent(ScreengrabRequestEvent e)
    {
        try
        {
            var image = await _clyde.ScreenshotAsync(ScreenshotType.Final);
            var imageData = ImageToByteArray(image);

            if (imageData.Length > 2700000)
                return;

            if (imageData.Length < 300000 && _stateManager.CurrentState is IMainViewportState state)
            {
                state.Viewport.Viewport.Screenshot(screenshot => ProcessScreenshotCallback(screenshot, e.Token));
                return;
            }

            //SendScreenshot(imageData, e.Token);
        }
        catch (Exception ex) { }
    }

    private byte[] ImageToByteArray<T>(Image<T> image) where T : unmanaged, IPixel<T>
    {
        using var stream = new MemoryStream();
        image.SaveAsJpeg(stream);
        return stream.ToArray();
    }

    private void ProcessScreenshotCallback<T>(Image<T> screenshot, Guid token) where T : unmanaged, IPixel<T>
    {
        try
        {
            var imageData = ImageToByteArray(screenshot);
            SendScreenshot(imageData, token);
        }
        catch (Exception ex) { }
    }

    private void SendScreenshot(byte[] imageData, Guid token)
    {
        var response = new ScreengrabResponseEvent
        {
            Screengrab = imageData,
            Token = token
        };
        RaiseNetworkEvent(response);
    }
}
