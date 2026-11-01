using ConsoleScout.Contracts;
using ConsoleScout.Agent.Browser;
using Microsoft.Playwright;

namespace ConsoleScout.Agent.Search;

/// <summary>Google results read from the rendered results page. Google serves no results without
/// JavaScript, and its class names are obfuscated and rotate, so results are found by structure instead:
/// an organic result is a link wrapping an h3 inside #search. A CAPTCHA is reported, never worked around.</summary>
internal sealed class GoogleEngine(BrowserHost browser) : ISearchEngine
{
    // The snippet usually lives in a line-clamped block; .VwiC3b has marked it for years. When neither
    // is found, the result block's text minus the title stands in.
    private const string Script =
        """
        (max) => {
          const hits = [];
          const seen = new Set();
          for (const a of document.querySelectorAll('#search a[href]')) {
            const h3 = a.querySelector('h3');
            if (!h3) continue;
            let url = a.href;
            try {
              const u = new URL(url);
              if (u.hostname.startsWith('www.google.') && u.pathname === '/url')
                url = u.searchParams.get('q') || u.searchParams.get('url') || url;
            } catch { continue; }
            if (!/^https?:/.test(url) || seen.has(url)) continue;
            seen.add(url);
            const block = a.closest('[data-hveid]') || a.parentElement;
            const clamp = block && block.querySelector('.VwiC3b, [data-sncf], div[style*="line-clamp"]');
            let snippet = clamp ? clamp.innerText : (block ? block.innerText.replace(h3.innerText, '') : '');
            hits.push({ url, title: h3.innerText.trim(), snippet: snippet.replace(/\s+/g, ' ').trim().slice(0, 500) });
            if (hits.length >= max) break;
          }
          return JSON.stringify(hits);
        }
        """;

    public string Name => "google";

    public Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int maxResults, CancellationToken ct) =>
        browser.WithPageAsync(async page =>
        {
            var url = $"https://www.google.com/search?q={Uri.EscapeDataString(query)}&num={Math.Clamp(maxResults + 2, 5, 20)}";
            await page.GotoAsync(url, new() { WaitUntil = WaitUntilState.DOMContentLoaded });

            EnsureNotBlocked(page.Url);

            try
            {
                await page.WaitForSelectorAsync("#search a h3, #captcha-form, form[action*='sorry']", new() { Timeout = 10_000 });
            }
            catch (TimeoutException)
            {
                // A results page with nothing to show has no h3 either; read what is there.
            }

            EnsureNotBlocked(page.Url);
            if (await page.QuerySelectorAsync("#captcha-form, form[action*='sorry']") is not null)
            {
                throw Captcha();
            }

            var json = await page.EvaluateAsync<string>(Script, maxResults);
            return SearchEngineExtensions.ParseHits(json);
        }, ct);

    private static void EnsureNotBlocked(string url)
    {
        var uri = new Uri(url);

        if (uri.AbsolutePath.StartsWith("/sorry", StringComparison.Ordinal))
        {
            throw Captcha();
        }

        if (uri.Host.StartsWith("consent.", StringComparison.OrdinalIgnoreCase))
        {
            throw new AgentFailure(AgentErrorCodes.Blocked,
                "Google asked for cookie consent. Run the agent once with Browser:Headless=false and accept it; "
                + "the choice is kept in the browser profile.");
        }
    }

    private static AgentFailure Captcha() =>
        new(AgentErrorCodes.Captcha, "Google answered with a CAPTCHA (unusual traffic). Try again later or use another engine.");
}
