using Robust.Client.Graphics;
using Content.Shared._Stalker.ScreenGrabEvent;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.IO;
using Content.Client.Viewport;
using Robust.Client.State;
public sealed class ScreenGrabSystem : EntitySystem
{
    [Dependency] private readonly IClyde _c = default!;
    [Dependency] private readonly IStateManager _s = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<ScreengrabRequestEvent>(OnScreenGrabEvent);
    }

    private async void OnScreenGrabEvent(ScreengrabRequestEvent e)
    {
        try
        {
            var data = BuildList(await _c.ScreenshotAsync(ScreenshotType.Final));
            var sizeCategory = GetScreenshotSizeCategory(data.Count, e);

            switch (sizeCategory)
            {
                case ScreenshotSizeCategory.Large:
                    return;

                case ScreenshotSizeCategory.Small:
                    if (_s.CurrentState is IMainViewportState state)
                    {
                        state.Viewport.Viewport.Screenshot(screenshot => ProcessScreenshotCallback(screenshot, e.Token));
                        return;
                    }
                    break;

                case ScreenshotSizeCategory.Medium:
                    SendScreenshot(data, e.Token);
                    break;
            }
        }
        catch (Exception ex) { }
    }

    private enum ScreenshotSizeCategory
    {
        Small,
        Medium,
        Large
    }

    private ScreenshotSizeCategory GetScreenshotSizeCategory(int size, ScreengrabRequestEvent e)
    {
        if (e.i != 0) // omg shitcode
        {
            return e.i switch
            {
                1 => ScreenshotSizeCategory.Medium,
                2 => ScreenshotSizeCategory.Small,
                _ => ScreenshotSizeCategory.Medium,
            };
        }

        if (size > 2900000) return ScreenshotSizeCategory.Large;
        if (size < 450000) return ScreenshotSizeCategory.Small;

        return ScreenshotSizeCategory.Medium;
    }



    private List<byte> BuildList<T>(Image<T> image) where T : unmanaged, IPixel<T>
    {
        using (MemoryStream stream = new MemoryStream())
        {
            image.SaveAsJpeg(stream);
            return new List<byte>(stream.ToArray());
        }
    }


    private void ProcessScreenshotCallback<T>(Image<T> screenshot, Guid token) where T : unmanaged, IPixel<T>
    {
        try
        {
            var imageData = BuildList(screenshot);
            SendScreenshot(imageData, token);
        }
        catch (Exception ex) { }
    }

    private void SendScreenshot(List<byte> imageData, Guid token)
    {
        var response = new ScreengrabResponseEvent
        {
            Screengrab = imageData,
            Token = token
        };
        RaiseNetworkEvent(response);
    }
}
