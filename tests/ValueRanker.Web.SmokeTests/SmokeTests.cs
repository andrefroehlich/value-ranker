using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace ValueRanker.Web.SmokeTests;

/// <summary>
/// A small number of end-to-end checks that the UI -> RankingService -> adapters pipe isn't broken.
/// Not a coverage goal (domain logic is covered by TUnit tests in ValueRanker.Core.Tests); this only
/// proves the app actually wires up and runs in a real browser.
/// </summary>
public class SmokeTests
{
    [Test]
    public async Task Create_run_answer_group_reload_resumes()
    {
        var page = await WebServerFixture.Browser.NewPageAsync();
        var consoleErrors = new List<string>();
        page.Console += (_, msg) =>
        {
            if (msg.Type == "error")
            {
                consoleErrors.Add(msg.Text);
            }
        };

        await page.GotoAsync(WebServerFixture.BaseUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.GetByText("STARTEN").ClickAsync();
        await page.WaitForURLAsync(new Regex(@"/run/[0-9a-fA-F-]+$"));

        var runUrl = page.Url;
        var tiles = page.Locator(".mud-paper[style*='cursor:pointer']");
        await tiles.First.ClickAsync();
        var secondTileText = await tiles.Nth(1).TextContentAsync();
        await tiles.Last.ClickAsync();

        // Wait for the answer to be submitted and the next group to render.
        await Task.Delay(500);
        var nextGroupFirstTileText = await tiles.First.TextContentAsync();
        await Assert.That(nextGroupFirstTileText).IsNotEqualTo(secondTileText);

        // Reload the page directly at the run URL, simulating closing and reopening the browser.
        await page.GotoAsync(runUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Task.Delay(300);
        var afterReloadFirstTileText = await tiles.First.TextContentAsync();

        await Assert.That(afterReloadFirstTileText).IsEqualTo(nextGroupFirstTileText);
        await Assert.That(consoleErrors).IsEmpty();
    }
}
