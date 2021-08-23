using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using System.Runtime.InteropServices;
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
    new Argument<int[]>("port", () => new [] { 4242 }, "Port(s) to listen on"),
    new Option<bool>("--http2", @"Use HTTP/2 only. Prevents additional port for HTTP/2 to support gRPC.")
}.WithConfigurableDefaults("echo");

command.Handler = CommandHandler.Create<bool, int[], CancellationToken>(
    async (http2, port, cancellation) => await RunAsync(args, port, http2, cancellation));

return await command.InvokeAsync(args);

static async Task RunAsync(string[] args, int[] ports, bool http2, CancellationToken cancellation)
{
    AnsiConsole.MarkupLine($"[grey]Runtime: {RuntimeInformation.FrameworkDescription}[/]");

    // NOTE: HTTP/3 work in progress for now. See https://github.com/dotnet/aspnetcore/projects/19#card-64856371
    var http = http2 == true ? HttpProtocols.Http2 : HttpProtocols.Http1AndHttp2;

    await Host.CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(builder =>
        {
            builder.ConfigureKestrel(opt =>
            {
                foreach (var port in ports)
                {
                    opt.ListenLocalhost(port, o => o.Protocols = http);
                    if (http2 != true)
                    {
                        // Also register port+1 exclusively for grpc, which is required for non-TLS connection
                        // See https://docs.microsoft.com/en-US/aspnet/core/grpc/troubleshoot?view=aspnetcore-5.0#unable-to-start-aspnet-core-grpc-app-on-macos
                        // "When an HTTP/2 endpoint is configured without TLS, the endpoint's ListenOptions.Protocols must be set to
                        // HttpProtocols.Http2. HttpProtocols.Http1AndHttp2 can't be used because TLS is required to negotiate HTTP/2.
                        // Without TLS, all connections to the endpoint default to HTTP/1.1, and gRPC calls fail."
                        // "HTTP/2 without TLS should only be used during app development. Production apps should always use transport security."
                        opt.ListenLocalhost(port + 1, o => o.Protocols = HttpProtocols.Http2);
                        AnsiConsole.MarkupLine($"[grey]gRPC HTTP/2 port: {port + 1}[/]");
                    }
                }
            });
            builder.UseStartup<Startup>();
        })
        .Build()
        .RunAsync(cancellation);
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