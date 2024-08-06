using System.Collections.Concurrent;

namespace QrRedirect.Services;

public class RedirectManagerService
{
    public const string SuccessMessage = "Redirected";
    public const string NotFoundMessage = "Subject not found to redirect";
    public const string Invalid = "Invalid Link";
    private ConcurrentDictionary<string, RedirectPage> pages = new();

    public static string Combine(string uri1, string uri2)
    {
        uri1 = uri1.TrimEnd('/');
        uri2 = uri2.TrimStart('/');
        return string.Format("{0}/{1}", uri1, uri2);
    }

    public bool Exist(string id) =>
        pages.ContainsKey(id);

    public void Unregister(string id)
    {
        pages.TryRemove(id, out _);
    }

    public RedirectPage Register(string id)
    {
        if (pages.TryGetValue(id, out var page))
            return page;

        page = new RedirectPage() { Id = id };
        pages.TryAdd(id, page);
        return page;
    }

    public async Task<string> Submit(string id, string link)
    {
        if (!pages.TryGetValue(id, out var page))
            return NotFoundMessage;

        if (page.Redirect is null)
            return NotFoundMessage;

        if (!ValidateLink(link, out var uri))
            return Invalid;

        await page.Redirect.Invoke(uri.ToString());
        return SuccessMessage;
    }

    private bool ValidateLink(string link, out Uri uri)
    {
        if (!link.StartsWith("http"))
            link = "https://" + link;
        return Uri.TryCreate(link, UriKind.Absolute, out uri);
    }
}

public class RedirectPage
{
    public string Id { get; set; }
    public Func<string, Task>? Redirect { get; set; }
}