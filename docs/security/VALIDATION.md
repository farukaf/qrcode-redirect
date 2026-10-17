# .NET 10 migration and security validation

Audit date: 2026-10-17. Branch: `upgrade/dotnet10-security`, initialized with `gh stack` from `main`.

The application now targets .NET 10 with SDK 10.0.402 / ASP.NET runtime 10.0.13. QRCoder was updated from 1.6.0 to 1.8.1; vendored Bootstrap CSS and source map were refreshed from upstream 5.3.8. NuGet lock files cover application and test dependencies. Server-side redirect capabilities use cryptographic 32-character identifiers, replacing predictable five-character `Random` identifiers. Redirect submission accepts HTTP(S) only and rejects credentials in the URL.

The image uses the Microsoft Azure Linux 3.0 distroless runtime, pinned by multiarchitecture manifest digest, running as the built-in unprivileged app user on port 8080. Ubuntu Noble chiseled 10.0.13 was evaluated first but retained two unpatched glibc advisories (CVE-2026-18374, CVE-2026-89092); Azure Linux removed those findings. The SDK image is also pinned by digest. Workflow actions use immutable commit hashes, read-only repository permissions, and Docker credentials passed through stdin.

## Repeat verification

```sh
dotnet restore tests/QrRedirect.Tests/QrRedirect.Tests.csproj --locked-mode
dotnet build tests/QrRedirect.Tests/QrRedirect.Tests.csproj -c Release --no-restore --warnaserror
dotnet test tests/QrRedirect.Tests/QrRedirect.Tests.csproj -c Release --no-build --filter 'Category!=Browser'
dotnet format analyzers tests/QrRedirect.Tests/QrRedirect.Tests.csproj --verify-no-changes --no-restore --severity warn
dotnet package list --project tests/QrRedirect.Tests/QrRedirect.Tests.csproj --vulnerable --include-transitive --format json
docker build -t qrcode-redirect:dotnet10 .
docker run -d --name qr-smoke -p 5180:8080 qrcode-redirect:dotnet10
QR_BASE_URL=http://localhost:5180 dotnet test tests/QrRedirect.Tests/QrRedirect.Tests.csproj -c Release --no-build --filter 'Category=Browser'
trivy image --scanners vuln qrcode-redirect:dotnet10
```

Selenium WebDriver/Support 4.51.0 runs headless Chrome via Selenium Manager. Set `CHROME_BINARY` to override the installed browser, and absolute `QR_ARTIFACTS` for failure screenshot/HTML/browser logs. The browser test starts at the rendered QR PNG, opens its form in another tab, rejects a script URI, and checks that successful submission navigates the original tab. WebApplicationFactory additionally exercises both Razor pages and the real Blazor negotiation endpoint; URL validation has positive and negative tests. Browser tests are explicitly skipped without `QR_BASE_URL`; CI starts the built image and runs them. CI uploads diagnostics after failures.

This host required `docker build --network host` and a runtime with `--network host -e ASPNETCORE_URLS=http://0.0.0.0:5180`: its Docker bridge forwarding was unavailable. This does not change the normal port-8080 image contract.

Trivy 0.76.0 scanned the built image and repository (NuGet locks, Dockerfile, secrets). No known package/image vulnerabilities or repository secrets were found in the attached reports. DS-0026 reports the missing Dockerfile HEALTHCHECK; a distroless image has no curl/shell, and the deployment should supply an HTTP readiness probe. This is a configuration observation, not a claim of a passed deployment health gate.

GitHub's Dependabot alerts endpoint returned 403 because alerts are disabled. Code scanning returned 404 because no analysis exists. Secret scanning returned an empty list. GitHub advisory API queries for Bootstrap 5.3.8 and QRCoder 1.8.1 returned no matches. These checks do not establish the absence of unknown vulnerabilities. Raw reports are in `evidence/`.

## Activate Sonar Cloud

The repository is public under MIT and can use the Sonar Cloud OSS offering. Create/import its project at https://sonarcloud.io, disable Automatic Analysis (the .NET scanner runs in CI), set repository variables `SONAR_ORGANIZATION`, `SONAR_PROJECT_KEY`, and secret `SONAR_TOKEN`, then set variable `SONAR_ENABLED=true`. Scanner 12.0.0 is pinned in `dotnet-tools.json`. The workflow collects OpenCover data and waits for the server quality gate. Missing configuration fails when enabled; fork PRs cannot access the token and are excluded. Until enabled, CI explicitly reports that no Sonar analysis or quality gate ran. Local compiler/analyzer checks are independent of Sonar.

Primary sources: [Microsoft .NET container tags](https://github.com/dotnet/dotnet-docker), [QRCoder NuGet](https://www.nuget.org/packages/QRCoder/1.8.1), [Bootstrap 5.3.8](https://blog.getbootstrap.com/2025/08/25/bootstrap-5-3-8/), [Selenium Manager](https://www.selenium.dev/documentation/selenium_manager/), [SonarScanner for .NET](https://docs.sonarsource.com/sonarqube-cloud/advanced-setup/ci-based-analysis/sonarscanner-for-dotnet/).

Diagnostics were verified during an initial browser hydration failure: screenshot, page HTML and browser log were written. The test now waits for interactive rendering and retries detached elements; diagnostic failures preserve the original assertion failure.
