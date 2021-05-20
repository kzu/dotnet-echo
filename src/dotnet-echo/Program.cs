using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DotNet;
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
    new Option<string[]>(new[] { "--prefix", "-p" }, () => new [] { "http://127.0.0.1:8080/" }, "Prefix to listen on such as http://127.0.0.0:8080/")
}.WithConfigurableDefaults("echo");

command.Handler = CommandHandler.Create<string[], CancellationToken>(
    async (prefix, cancellation)  => await RunAsync(args, prefix, cancellation));

return await command.InvokeAsync(args);

static async Task RunAsync(string[] args, string[] prefixes, CancellationToken cancellation)
{
    await Host.CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(builder =>
        {
            builder.ConfigureKestrel(opt =>
            {
                foreach (var prefix in prefixes)
                {
                    if (!Uri.TryCreate(prefix, UriKind.Absolute, out var uri))
                    {
                        AnsiConsole.MarkupLine($"[yellow]Invalid URI {prefix}. Skipping.[/]");
                        continue;
                    }
                    if (!IPAddress.TryParse(uri.Host, out var ip))
                    {
                        AnsiConsole.MarkupLine($"[yellow]Invalid IPAddress {uri.Host}. Skipping.[/]");
                        continue;
                    }
                    opt.Listen(ip, uri.Port, o =>
                    {
                        o.Protocols = HttpProtocols.Http1;
                        if (uri.Scheme == "https")
                            o.UseHttps();
                    });
                    // For gRPC, we don't setup SSL. Also, we listen on a different port.
                    // See https://docs.microsoft.com/en-us/aspnet/core/grpc/troubleshoot?view=aspnetcore-5.0#unable-to-start-aspnet-core-grpc-app-on-macos
                    opt.Listen(ip, uri.Port + 1, o => o.Protocols = HttpProtocols.Http2);
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