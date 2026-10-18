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

    public void Unregister(RedirectPage page)
    {
        pages.TryRemove(new KeyValuePair<string, RedirectPage>(page.Id, page));
    }

    public bool TryRegister(string id, out RedirectPage page)
    {
        page = new RedirectPage { Id = id };
        return pages.TryAdd(id, page);
    }

    public RedirectPage Register(string id) =>
        pages.GetOrAdd(id, key => new RedirectPage { Id = key });

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

    private static bool ValidateLink(string link, out Uri uri)
    {
        if (!link.Contains(':'))
            link = "https://" + link;
        if (Uri.TryCreate(link, UriKind.Absolute, out var parsed)
            && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps)
            && !string.IsNullOrWhiteSpace(parsed.Host) && string.IsNullOrEmpty(parsed.UserInfo))
        {
            uri = parsed;
            return true;
        }
        uri = null!;
        return false;
    }
}

public class RedirectPage
{
    public string Id { get; set; } = string.Empty;
    public Func<string, Task>? Redirect { get; set; }
}