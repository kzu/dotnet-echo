using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Devlooped;
using DotNetConfig;
using Humanizer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Hosting;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Spectre.Console;

#if !CI
AnsiConsole.MarkupLine($"[lime]dotnet echo [/]{string.Join(' ', args)}");
#endif

var config = Config.Build().GetSection("echo");
// Check for updates once a day.
var check = config.GetDateTime("checked");
// Only check for CI builds
if (ThisAssembly.Project.CI.Equals("true", StringComparison.OrdinalIgnoreCase) &&
    (check == null || (DateTime.Now - check) > 24.Hours()))
{
    var update = await GetUpdateAsync();
    config.SetDateTime("checked", DateTime.Now);
    if (update != null)
        AnsiConsole.MarkupLine($"[yellow]New version v{update.Identity.Version} from {(DateTimeOffset.Now - (update.Published ?? DateTimeOffset.Now)).Humanize()} ago is available.[/] Update with: [lime]dotnet tool update -g dotnet-echo[/]");
}

var command = new RootCommand("A trivial program that echoes whatever is sent to it via HTTP.")
{
    new Argument<int[]>("port", "Port(s) to listen on. [default: 80 or 443 with --ssl]"),
    new Option<bool>(new[] { "--ssl", "-ssl" }, @"Use HTTPS with self-signed SSL certificate, persisted as dotnet-echo.pfx in the current directory."),
    new Option<bool>(new[] { "--http2", "-http2" }, @"Use HTTP/2 only. Prevents additional port for HTTP/2 to support gRPC."),
}.WithConfigurableDefaults("echo");

command.Handler = CommandHandler.Create<bool, bool, int[], CancellationToken>(
    async (ssl, http2, port, cancellation) => await RunAsync(args, port, ssl, http2, cancellation));

return await command.InvokeAsync(args);

static async Task RunAsync(string[] args, int[] ports, bool ssl, bool http2, CancellationToken cancellation)
{
    AnsiConsole.MarkupLine($"[grey]Runtime: {RuntimeInformation.FrameworkDescription}[/]");
    if (ports.Length == 0)
        ports = new[] { ssl ? 443 : 80 };

    var cert = ssl ? GetSelfSignedCertificate() : default;

    // NOTE: HTTP/3 work in progress for now. See https://github.com/dotnet/aspnetcore/projects/19#card-64856371
    var http = http2 == true ? HttpProtocols.Http2 : HttpProtocols.Http1AndHttp2;

    await Host.CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(builder =>
        {
            builder.ConfigureKestrel(opt =>
            {
                foreach (var port in ports)
                {
                    opt.ListenLocalhost(port, o =>
                    {
                        o.Protocols = http;
                        if (ssl)
                            o.UseHttps(cert);
                    });
                    if (http2 != true)
                    {
                        // Also register port+1 exclusively for grpc, which is required for non-TLS connection
                        // See https://docs.microsoft.com/en-US/aspnet/core/grpc/troubleshoot?view=aspnetcore-5.0#unable-to-start-aspnet-core-grpc-app-on-macos
                        // "When an HTTP/2 endpoint is configured without TLS, the endpoint's ListenOptions.Protocols must be set to
                        // HttpProtocols.Http2. HttpProtocols.Http1AndHttp2 can't be used because TLS is required to negotiate HTTP/2.
                        // Without TLS, all connections to the endpoint default to HTTP/1.1, and gRPC calls fail."
                        // "HTTP/2 without TLS should only be used during app development. Production apps should always use transport security."
                        opt.ListenLocalhost(port + 1, o =>
                        {
                            o.Protocols = HttpProtocols.Http2;
                            if (ssl)
                                o.UseHttps(cert);
                        });

                        AnsiConsole.MarkupLine($"[grey]gRPC HTTP/2 port: {port + 1}[/]");
                    }
                }
            });
            builder.UseStartup<Startup>();
        })
        .Build()
        .RunAsync(cancellation);
}

static X509Certificate2 GetSelfSignedCertificate()
{
    byte[] pfx;

    if (File.Exists("dotnet-echo.pfx"))
    {
        pfx = File.ReadAllBytes("dotnet-echo.pfx");
    }
    else
    {
        var commonName = "Devlooped";
        var rsaKeySize = 2048;
        var years = 5;
        var hashAlgorithm = HashAlgorithmName.SHA256;

        var rsa = RSA.Create(rsaKeySize);
        var request = new CertificateRequest($"cn={commonName}", rsa, hashAlgorithm, RSASignaturePadding.Pkcs1)
        {
            CertificateExtensions =
        {
            new X509KeyUsageExtension(X509KeyUsageFlags.DataEncipherment | X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DigitalSignature, false),
            new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false),
        }
        };

        var certificate = request.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(years));
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            certificate.FriendlyName = commonName;

        // Return the PFX exported version that contains the key
        pfx = certificate.Export(X509ContentType.Pfx);
        File.WriteAllBytes("dotnet-echo.pfx", pfx);
    }

    return new X509Certificate2(pfx, default(string), X509KeyStorageFlags.MachineKeySet);
}

static Task<IPackageSearchMetadata?> GetUpdateAsync() => AnsiConsole.Status().StartAsync("Checking for updates", async context =>
{
    var providers = Repository.Provider.GetCoreV3();
    var source = new PackageSource("https://api.nuget.org/v3/index.json");
    var repo = new SourceRepository(source, providers);
    var resource = await repo.GetResourceAsync<PackageMetadataResource>();
    var metadata = await resource.GetMetadataAsync(ThisAssembly.Project.PackageId, false, false, new SourceCacheContext(), NuGet.Common.NullLogger.Instance, CancellationToken.None);

    var update = metadata
        //.Select(x => x.Identity)
        .Where(x => x.Identity.Version > new NuGetVersion(ThisAssembly.Project.Version))
        .OrderByDescending(x => x.Identity.Version)
        //.Select(x => x.Version)
        .FirstOrDefault();

    return update;
});