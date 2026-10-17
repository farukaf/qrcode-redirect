using StbImageSharp;
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
        using var formDriver = new ChromeDriver(options);
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(30));
        wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
        var baseUrl = Environment.GetEnvironmentVariable("QR_BASE_URL")!.TrimEnd('/');
        try
        {
            driver.Navigate().GoToUrl(baseUrl);
            wait.Until(d => d.FindElements(By.Id("qr-code")).Count == 1);
            // The server prerender and interactive circuit initialize separately.
            wait.Until(d => d.FindElement(By.Id("qr-code")).GetAttribute("data-interactive") == "true");
            var pngSource = driver.FindElement(By.Id("qr-code")).GetAttribute("src")!;
            const string pngPrefix = "data:image/png;base64,";
            Assert.StartsWith(pngPrefix, pngSource);
            var png = ImageResult.FromMemory(Convert.FromBase64String(pngSource[pngPrefix.Length..]), ColorComponents.RedGreenBlue);
            var decoded = new ZXing.BarcodeReaderGeneric().Decode(
                new ZXing.RGBLuminanceSource(png.Data, png.Width, png.Height, ZXing.RGBLuminanceSource.BitmapFormat.RGB24));
            Assert.NotNull(decoded);
            Assert.Equal(ZXing.BarcodeFormat.QR_CODE, decoded.BarcodeFormat);
            var formUrl = decoded.Text;
            Assert.StartsWith(baseUrl + "/f/", formUrl);
            Assert.Matches(@"/f/[A-Za-z0-9]{32}$", formUrl);
            Assert.Equal(formUrl, driver.FindElement(By.CssSelector("a[href*='/f/']")).GetAttribute("href"));
            Assert.NotEqual(driver.SessionId, formDriver.SessionId);
            formDriver.Navigate().GoToUrl(formUrl);
            var formWait = new WebDriverWait(formDriver, TimeSpan.FromSeconds(30));
            formWait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
            formWait.Until(d => d.FindElement(By.CssSelector("form")).GetAttribute("data-interactive") == "true");
            formWait.Until(d =>
            {
                if (d.PageSource.Contains("Invalid Link")) return true;
                var field = d.FindElement(By.Id("link"));
                field.Clear();
                field.SendKeys("javascript:alert(1)");
                d.FindElement(By.CssSelector("button[type=submit]")).Click();
                return false;
            });
            var input = formDriver.FindElement(By.Id("link"));
            input.Clear();
            input.SendKeys(baseUrl + "/f/completed");
            formDriver.FindElement(By.CssSelector("button[type=submit]")).Click();
            formWait.Until(d => d.PageSource.Contains("Page redirected"));
            wait.Until(d => d.Url == baseUrl + "/f/completed");
            Assert.Contains("Link:", driver.PageSource);
        }
        catch
        {
            CaptureFailure(driver, "origin");
            CaptureFailure(formDriver, "form");
            throw;
        }
    }

    private static void CaptureFailure(ChromeDriver driver, string session)
    {
        var root = Environment.GetEnvironmentVariable("QR_ARTIFACTS") ?? "TestResults/browser";
        var path = Path.Combine(root, session);
        try
        {
            Directory.CreateDirectory(path);
            try { driver.GetScreenshot().SaveAsFile(Path.Combine(path, "failure.png")); }
            catch (WebDriverException captureError) { File.WriteAllText(Path.Combine(path, "capture-error.log"), captureError.ToString()); }
            try { File.WriteAllText(Path.Combine(path, "page.html"), driver.PageSource); }
            catch (WebDriverException) { /* Preserve the original test failure. */ }
            try { File.WriteAllText(Path.Combine(path, "browser.log"), string.Join(Environment.NewLine, driver.Manage().Logs.GetLog(LogType.Browser))); }
            catch (WebDriverException) { /* Logging support varies by browser. */ }
        }
        catch (IOException) { /* Diagnostic I/O must not hide the original test failure. */ }
        catch (UnauthorizedAccessException) { /* Diagnostic I/O must not hide the original test failure. */ }
    }
}
