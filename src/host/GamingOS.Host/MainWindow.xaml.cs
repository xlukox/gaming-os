using System.IO;
using System.Windows;
using GamingOS.Activity;
using GamingOS.Core;
using GamingOS.Intel;
using GamingOS.Library;
using Microsoft.Web.WebView2.Core;

namespace GamingOS.Host;

/// <summary>Borderless full-screen window that hosts the React UI in WebView2 and answers its requests.</summary>
public partial class MainWindow : Window
{
    private const string UiOrigin = "https://app.gamingos.local";
    private readonly IpcRouter _router = new();

    public MainWindow()
    {
        InitializeComponent();
        WebView.DefaultBackgroundColor = System.Drawing.Color.FromArgb(7, 6, 22);

        LibraryModule.Register(_router);
        IntelModule.Register(_router);

        Loaded += async (_, _) =>
        {
            try
            {
                await StartAsync();
            }
            catch (Exception e)
            {
                MessageBox.Show(this, e.Message, "Gaming OS", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        };
    }

    private async Task StartAsync()
    {
        // GAMINGOS_DATA_DIR lets several dev instances (git worktrees) run without sharing data.
        var dataDir = Environment.GetEnvironmentVariable("GAMINGOS_DATA_DIR")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GamingOS");
        ActivityModule.Start(new Database(Path.Combine(dataDir, "gamingos.db")), LibraryModule.InstalledGames);
        var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: Path.Combine(dataDir, "WebView2"));
        await WebView.EnsureCoreWebView2Async(environment);
        var web = WebView.CoreWebView2;

        // GAMINGOS_UI_URL points the window at the Vite dev server (http://localhost:5173) during UI work.
        var devUrl = Environment.GetEnvironmentVariable("GAMINGOS_UI_URL");
        var origin = devUrl is null ? UiOrigin : new Uri(devUrl).GetLeftPart(UriPartial.Authority);

        // Trust boundary: only our own UI may talk to the host or stay loaded in the window.
        web.NavigationStarting += (_, e) => e.Cancel = !IsFrom(origin, e.Uri) && !e.Uri.StartsWith("data:", StringComparison.Ordinal);
        web.NewWindowRequested += (_, e) => e.Handled = true;
        web.WebMessageReceived += async (_, e) =>
        {
            if (!IsFrom(origin, e.Source)) return;
            var response = await _router.HandleAsync(e.WebMessageAsJson);
            if (response is not null) web.PostWebMessageAsJson(response);
        };

        // Game artwork already on disk. DenyCors lets <img> and CSS show it but keeps scripts from reading the files.
        foreach (var (host, folder) in LibraryModule.ArtworkFolders())
            web.SetVirtualHostNameToFolderMapping(host, folder, CoreWebView2HostResourceAccessKind.DenyCors);

        if (devUrl is not null)
        {
            web.Navigate(devUrl);
            return;
        }

        var uiDir = Path.Combine(AppContext.BaseDirectory, "ui");
        if (!File.Exists(Path.Combine(uiDir, "index.html")))
        {
            web.NavigateToString(
                "<body style='background:#070616;color:#f1eeff;font:20px Segoe UI;padding:48px'>" +
                "UI build not found. Run <b>npm run build</b> in src/ui, then build the host again.</body>");
            return;
        }

        web.SetVirtualHostNameToFolderMapping(new Uri(UiOrigin).Host, uiDir, CoreWebView2HostResourceAccessKind.Deny);
        web.Navigate($"{UiOrigin}/index.html");
    }

    private static bool IsFrom(string origin, string uri) =>
        Uri.TryCreate(uri, UriKind.Absolute, out var parsed)
        && string.Equals(parsed.GetLeftPart(UriPartial.Authority), origin, StringComparison.OrdinalIgnoreCase);
}
