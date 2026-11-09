using System.Net;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace PallsPaper;

internal sealed class WallpaperWindow : Form
{
    private const string VirtualHostName = "pallspaper.local";
    private static readonly HashSet<string> NativeImageExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".bmp", ".png"
        };

    public static bool UsesNativeImageRenderer(string source, bool isWebAddress) =>
        !isWebAddress && NativeImageExtensions.Contains(Path.GetExtension(source));

    public WallpaperWindow()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
    }

    protected override bool ShowWithoutActivation => true;

    public async Task LoadWallpaperAsync(
        CoreWebView2Environment? environment,
        string source,
        bool isWebAddress)
    {
        var extension = Path.GetExtension(source).ToLowerInvariant();
        if (UsesNativeImageRenderer(source, isWebAddress))
        {
            Controls.Add(new ImageWallpaperView(source) { Dock = DockStyle.Fill });
            return;
        }

        if (environment is null)
        {
            throw new InvalidOperationException("Die WebView2-Umgebung wurde nicht initialisiert.");
        }

        var browser = new WebView2 { Dock = DockStyle.Fill };
        Controls.Add(browser);
        await browser.EnsureCoreWebView2Async(environment);

        var core = browser.CoreWebView2;
        browser.ZoomFactor = 1;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.AreBrowserAcceleratorKeysEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsZoomControlEnabled = false;

        if (isWebAddress)
        {
            core.Navigate(source);
            return;
        }

        var directory = Path.GetDirectoryName(source)
            ?? throw new InvalidOperationException("Der Ordner des ausgewählten Wallpapers konnte nicht ermittelt werden.");
        var fileName = Path.GetFileName(source);
        core.SetVirtualHostNameToFolderMapping(
            VirtualHostName,
            directory,
            CoreWebView2HostResourceAccessKind.Allow);

        var sourceUri = $"https://{VirtualHostName}/{Uri.EscapeDataString(fileName)}";
        if (extension is ".html" or ".htm")
        {
            core.Navigate(sourceUri);
            return;
        }

        if (extension is ".jpg" or ".jpeg" or ".gif")
        {
            await NavigateToLocalImageAsync(core, sourceUri, extension);
            return;
        }

        core.NavigateToString(CreateMediaDocument(sourceUri, extension));
    }

    private static async Task NavigateToLocalImageAsync(CoreWebView2 core, string sourceUri, string extension)
    {
        var navigationCompleted = new TaskCompletionSource<CoreWebView2NavigationCompletedEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        void HandleNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs eventArgs) =>
            navigationCompleted.TrySetResult(eventArgs);

        core.NavigationCompleted += HandleNavigationCompleted;
        try
        {
            core.NavigateToString(CreateMediaDocument(sourceUri, extension));
            var result = await navigationCompleted.Task.WaitAsync(TimeSpan.FromSeconds(15));
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException($"Das Wallpaper konnte nicht geladen werden: {result.WebErrorStatus}.");
            }

            var timeout = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < timeout)
            {
                var imageFailed = await core.ExecuteScriptAsync(
                    "document.body.dataset.wallpaperStatus === 'error'");
                if (imageFailed == "true")
                {
                    throw new InvalidOperationException("Das Bild konnte nicht aus der ausgewählten Datei geladen werden.");
                }

                var imageLoaded = await core.ExecuteScriptAsync(
                    "document.body.dataset.wallpaperStatus === 'loaded'");
                if (imageLoaded == "true")
                {
                    return;
                }

                await Task.Delay(50);
            }

            throw new TimeoutException("Das Bild wurde innerhalb von 15 Sekunden nicht geladen.");
        }
        finally
        {
            core.NavigationCompleted -= HandleNavigationCompleted;
        }
    }

    private static string CreateMediaDocument(string sourceUri, string extension)
    {
        var encodedUri = WebUtility.HtmlEncode(sourceUri);
        var mediaElement = extension switch
        {
            ".mp4" => $"<video src=\"{encodedUri}\" autoplay muted loop playsinline></video>",
            ".webm" => $"<video src=\"{encodedUri}\" autoplay muted loop playsinline></video>",
            ".ogv" => $"<video src=\"{encodedUri}\" autoplay muted loop playsinline></video>",
            _ => $"<img src=\"{encodedUri}\" alt=\"Wallpaper\" " +
                 "onload=\"document.body.dataset.wallpaperStatus='loaded'\" " +
                 "onerror=\"document.body.dataset.wallpaperStatus='error'\">"
        };

        return $$"""
            <!doctype html>
            <html>
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <style>
                * { box-sizing: border-box; }
                html, body { position: fixed; inset: 0; width: 100%; height: 100%; margin: 0; overflow: hidden; background: #000; }
                img, video { position: absolute; left: 0; top: 0; display: block; width: 100%; height: 100%; object-fit: fill; }
              </style>
            </head>
            <body>{{mediaElement}}</body>
            </html>
            """;
    }
}
