using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using QrRedirect.Services;

namespace QrRedirect.Tests;

public class ApplicationTests
{
    [Fact]
    public async Task HomeFormAndBlazorResolveServices()
    {
        await using var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var home = await client.GetStringAsync("/");
        Assert.Contains("data:image/png;base64,", home);
        Assert.Matches(@"/f/[A-Za-z0-9]{5}""", home);
        Assert.Contains("Link:", await client.GetStringAsync("/f/test"));
        var negotiate = await client.PostAsync("/_blazor/negotiate?negotiateVersion=1", null);
        Assert.Equal(HttpStatusCode.OK, negotiate.StatusCode);
        Assert.Contains("WebSockets", await negotiate.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CollidingCodeKeepsOriginalRegistrationAndCleanupRespectsOwnership()
    {
        var manager = new RedirectManagerService();
        const string code = "Ab123";
        Assert.True(manager.TryRegister(code, out var original));
        Assert.False(manager.TryRegister(code, out _));
        Assert.Same(original, manager.Register(code));
        string? redirected = null;
        original.Redirect = uri => { redirected = uri; return Task.CompletedTask; };
        Assert.Equal(RedirectManagerService.SuccessMessage, await manager.Submit(code, "example.com"));
        Assert.Equal("https://example.com/", redirected);

        manager.Unregister(original);
        Assert.False(manager.Exist(code));
        Assert.True(manager.TryRegister(code, out var replacement));
        manager.Unregister(original);
        Assert.True(manager.Exist(code));
        Assert.Same(replacement, manager.Register(code));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,hello")]
    [InlineData("file:///etc/passwd")]
    [InlineData("https://user:password@example.com")]
    [InlineData("http://")]
    public async Task UnsafeLinksDoNotInvokeRedirect(string link)
    {
        var manager = new RedirectManagerService();
        var page = manager.Register("test");
        page.Redirect = _ => throw new InvalidOperationException("Must not redirect");
        Assert.Equal(RedirectManagerService.Invalid, await manager.Submit("test", link));
    }

    [Theory]
    [InlineData("example.com", "https://example.com/")]
    [InlineData("HTTP://example.com", "http://example.com/")]
    [InlineData("https://example.com/path", "https://example.com/path")]
    public async Task WebLinksRedirect(string link, string expected)
    {
        var manager = new RedirectManagerService();
        string? redirected = null;
        manager.Register("test").Redirect = uri => { redirected = uri; return Task.CompletedTask; };
        Assert.Equal(RedirectManagerService.SuccessMessage, await manager.Submit("test", link));
        Assert.Equal(expected, redirected);
    }
}
