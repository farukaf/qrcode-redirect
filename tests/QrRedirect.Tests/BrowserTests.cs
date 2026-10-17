using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace QrRedirect.Tests;

public sealed class BrowserFactAttribute : FactAttribute
{
    public BrowserFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("QR_BASE_URL")))
            Skip = "Set QR_BASE_URL to test a running application with Chrome.";
    }
}

public class BrowserTests
{
    [BrowserFact]
    [Trait("Category", "Browser")]
    public void QrFormRejectsUnsafeLinksAndRedirectsOriginalBrowser()
    {
        var options = new ChromeOptions();
        options.AddArguments("--headless=new", "--no-sandbox", "--disable-dev-shm-usage");
        var binary = Environment.GetEnvironmentVariable("CHROME_BINARY");
        if (!string.IsNullOrEmpty(binary)) options.BinaryLocation = binary;
        using var driver = new ChromeDriver(options);
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(30));
        wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
        var baseUrl = Environment.GetEnvironmentVariable("QR_BASE_URL")!.TrimEnd('/');
        try
        {
            driver.Navigate().GoToUrl(baseUrl);
            wait.Until(d => d.FindElements(By.Id("qr-code")).Count == 1);
            // The server prerender and interactive circuit initialize separately.
            wait.Until(d => d.FindElement(By.Id("qr-code")).GetAttribute("data-interactive") == "true");
            var origin = driver.CurrentWindowHandle;
            var formUrl = driver.FindElement(By.Id("qr-code")).GetAttribute("data-test-value")!;
            Assert.Matches(@"/f/[A-Za-z0-9]{32}$", formUrl);
            Assert.StartsWith("data:image/png;base64,", driver.FindElement(By.Id("qr-code")).GetAttribute("src"));
            driver.SwitchTo().NewWindow(WindowType.Tab);
            driver.Navigate().GoToUrl(formUrl);
            wait.Until(d => d.FindElement(By.CssSelector("form")).GetAttribute("data-interactive") == "true");
            wait.Until(d =>
            {
                if (d.PageSource.Contains("Invalid Link")) return true;
                var field = d.FindElement(By.Id("link"));
                field.Clear();
                field.SendKeys("javascript:alert(1)");
                d.FindElement(By.CssSelector("button[type=submit]")).Click();
                return false;
            });
            var input = driver.FindElement(By.Id("link"));
            input.Clear();
            input.SendKeys(baseUrl + "/f/completed");
            driver.FindElement(By.CssSelector("button[type=submit]")).Click();
            wait.Until(d => d.PageSource.Contains("Page redirected"));
            driver.SwitchTo().Window(origin);
            wait.Until(d => d.Url == baseUrl + "/f/completed");
            Assert.Contains("Link:", driver.PageSource);
        }
        catch
        {
            var path = Environment.GetEnvironmentVariable("QR_ARTIFACTS") ?? "TestResults/browser";
            Directory.CreateDirectory(path);
            try { driver.GetScreenshot().SaveAsFile(Path.Combine(path, "failure.png")); }
            catch (WebDriverException captureError) { File.WriteAllText(Path.Combine(path, "capture-error.log"), captureError.ToString()); }
            try { File.WriteAllText(Path.Combine(path, "page.html"), driver.PageSource); }
            catch (WebDriverException) { /* Preserve the original test failure. */ }
            try { File.WriteAllText(Path.Combine(path, "browser.log"), string.Join(Environment.NewLine, driver.Manage().Logs.GetLog(LogType.Browser))); }
            catch (WebDriverException) { /* Logging support varies by browser. */ }
            throw;
        }
    }
}
