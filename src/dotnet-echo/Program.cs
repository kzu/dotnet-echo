using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DotNetConfig;
using Humanizer;
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
    new Option<string[]>(new[] { "--prefix", "-p" }, () => new [] { "http://*:8080/" }, "Prefix to listen on such as http://127.0.0.0:8080/")
}.WithConfigurableDefaults("echo");

command.Handler = CommandHandler.Create<string[], CancellationToken>(EchoAsync);

return await command.InvokeAsync(args);


static async Task EchoAsync(string[] prefix, CancellationToken cancellation)
{
    var http = new HttpListener();
    if (prefix.Length == 0)
    {
        http.Prefixes.Add("http://*:8080/");
    }
    else
    {
        foreach (var uri in prefix)
            http.Prefixes.Add(uri);
    }

    try
    {
        http.Start();
        AnsiConsole.WriteLine("Registered prefixes to listen on:");
        foreach (var p in prefix)
            AnsiConsole.WriteLine("    " + p);
    }
    catch (HttpListenerException ex) when (ex.ErrorCode == 5 && Environment.OSVersion.Platform == PlatformID.Win32NT)
    {
        if (prefix.Length == 1)
        {
            AnsiConsole.MarkupLine($"[red]Failed to acquire permissions to listen on the specified prefix [/][white on red]{prefix[0]}[/]");
            AnsiConsole.MarkupLine($"Either specify it as 'localhost' or '127.0.0.1' or run (as administrator): [yellow]netsh http add urlacl url={prefix[0]} user={Environment.UserName}[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]Failed to acquire permissions to listen on the specified prefixes [/][yellow]{string.Join(", ", prefix)}[/]");
            AnsiConsole.MarkupLine($"Either specify it as 'localhost' or '127.0.0.1' or run (as administrator):");
            foreach (var p in prefix)
                AnsiConsole.MarkupLine($"[yellow]netsh http add urlacl url={p} user={Environment.UserName}[/]");
        }
        return;
    }

    cancellation.Register(() => http.Stop());
    await AnsiConsole.Status().StartAsync("Listening", async _ =>
    {
        while (!cancellation.IsCancellationRequested)
        {
            try
            {
                // This call blocks until a request is made by the browser after auth, via the redirect
                var context = await http.GetContextAsync();
                AnsiConsole.MarkupLine($"[yellow]HTTP/{context.Request.ProtocolVersion} {context.Request.HttpMethod} [/][lime]{context.Request.RawUrl}[/]");
                AnsiConsole.MarkupLine($"[grey]    Content-Length: {context.Request.ContentLength64}[/]");
                AnsiConsole.MarkupLine($"[grey]    Content-Type: {context.Request.ContentType}[/]");
                AnsiConsole.MarkupLine($"[grey]    User-Agent: {context.Request.UserAgent}[/]");

                context.Response.ContentType = context.Request.ContentType;
                context.Response.ContentLength64 = context.Request.ContentLength64;

                var buffer = new byte[1024];
                var read = 0;
                while ((read = await context.Request.InputStream.ReadAsync(buffer, 0, buffer.Length, cancellation)) != 0)
                    await context.Response.OutputStream.WriteAsync(buffer, 0, read, cancellation);

                context.Response.Close();
            }
            catch (HttpListenerException e) when (e.ErrorCode == 995) { }
            catch (ObjectDisposedException) { }
        }
    });
}

static Task<IPackageSearchMetadata> GetUpdateAsync() => AnsiConsole.Status().StartAsync("Checking for updates", async context =>
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