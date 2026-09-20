using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Playwright;

namespace ValueRanker.Web.SmokeTests;

/// <summary>Starts the Blazor dev server and a headless browser once for the whole assembly.</summary>
public static class WebServerFixture
{
    public const string BaseUrl = "http://localhost:5299";

    private static Process? _serverProcess;
    private static IPlaywright? _playwright;

    public static IBrowser Browser { get; private set; } = null!;

    [Before(HookType.Assembly)]
    public static async Task StartAsync()
    {
        var webProjectPath = Path.Combine(FindRepoRoot(), "src", "ValueRanker.Web", "ValueRanker.Web.csproj");

        _serverProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{webProjectPath}\" --urls {BaseUrl}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };
        _serverProcess.Start();

        using var httpClient = new HttpClient();
        var deadline = DateTime.UtcNow.AddSeconds(90);

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var response = await httpClient.GetAsync(BaseUrl);
                if (response.IsSuccessStatusCode)
                {
                    break;
                }
            }
            catch (HttpRequestException)
            {
                // Server not up yet.
            }

            await Task.Delay(500);
        }

        // Fresh Linux CI runners are missing shared libraries headless Chromium needs; --with-deps installs them.
        var installArgs = RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            ? new[] { "install", "--with-deps", "chromium" }
            : new[] { "install", "chromium" };
        Microsoft.Playwright.Program.Main(installArgs);

        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync();
    }

    [After(HookType.Assembly)]
    public static async Task StopAsync()
    {
        await Browser.CloseAsync();
        _playwright?.Dispose();

        if (_serverProcess is { HasExited: false } process)
        {
            process.Kill(entireProcessTree: true);
        }

        _serverProcess?.Dispose();
    }

    private static string FindRepoRoot()
    {
        var directory = AppContext.BaseDirectory;

        while (directory is not null && !File.Exists(Path.Combine(directory, "ValueRanker.slnx")))
        {
            directory = Path.GetDirectoryName(directory);
        }

        return directory ?? throw new InvalidOperationException("Could not locate the repository root (ValueRanker.slnx) from the test output directory.");
    }
}
