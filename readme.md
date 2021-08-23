![Icon](https://raw.githubusercontent.com/kzu/dotnet-echo/main/assets/img/icon-32.png) dotnet-echo
============

[![Version](https://img.shields.io/nuget/v/dotnet-echo.svg?color=royalblue)](https://www.nuget.org/packages/dotnet-echo) [![Downloads](https://img.shields.io/nuget/dt/dotnet-echo.svg?color=darkmagenta)](https://www.nuget.org/packages/dotnet-echo) [![License](https://img.shields.io/github/license/kzu/dotnet-echo.svg?color=blue)](https://github.com/kzu/dotnet-echo/blob/main/LICENSE) [![CI Status](https://github.com/kzu/dotnet-file/workflows/build/badge.svg?branch=main)](https://github.com/kzu/dotnet-file/actions?query=branch%3Amain+workflow%3Abuild+) [![CI Version](https://img.shields.io/endpoint?url=https://shields.kzu.io/vpre/dotnet-echo/main&label=nuget.ci&color=brightgreen)](https://pkg.kzu.io/index.json)

Installing or updating (same command can be used for both):

```
dotnet tool update -g dotnet-echo
```

Usage:

```
> dotnet echo -?
echo
  A trivial program that echoes whatever is sent to it via HTTP or gRPC

Usage:
  echo [options] [<port>...]

Arguments:
  <port>  Port(s) to listen on [default: 4242]

Options:
  --http2         Use HTTP/2 only. Prevents additional port for HTTP/2 to support gRPC.
  --version       Show version information
  -?, -h, --help  Show help and usage information
```

The program will automatically check for updates once a day and recommend updating 
if there is a new version available.

The service supports gRPC too, with [echo.proto](src/dotnet-echo/echo.proto):

```protobuf
syntax = "proto3";

service chamber {
  rpc echo (message) returns (message);
}

message message {
  string payload = 1;
}
```

Since gRPC [needs to use HTTP/2](https://docs.microsoft.com/en-US/aspnet/core/grpc/troubleshoot?view=aspnetcore-5.0#unable-to-start-aspnet-core-grpc-app-on-macos), 
`dotnet-echo` will use the specified `port`(s) + 1 to listen HTTP/2-only traffic 
(i.e. if you specify `8080`, the gRPC endpoint will be available at `http://localhost:8081`). 
You can avoid the additional port by forcing HTTP/2-only with the `--http2` option.

Example of a .NET client to run `echo` in the `chamber` service:

```xml
<Project>
  ...
  <ItemGroup>
    <PackageReference Include="Google.Protobuf" Version="*" />
    <PackageReference Include="Grpc.Net.Client" Version="*" />
    <PackageReference Include="Grpc.Tools" Version="*" />
  </ItemGroup>
  <ItemGroup>
    <Protobuf Include="echo.proto" GrpcServices="Client" />
  </ItemGroup>
</Project>
```

```csharp
var channel = GrpcChannel.ForAddress("http://localhost:8081");
var service = new chamber.chamberClient(channel);

var response = await service.echoAsync(new message { Payload = "Hello World" }, cancellationToken: cancellation);

Console.WriteLine(response.Payload);
```

Example of a .NET client using HTTP/2 only mode for a regular HTTP POST:

```csharp
var http = new HttpClient();

var send = await http.SendAsync(new HttpRequestMessage(
    HttpMethod.Post,
    "http://localhost:8081")
    {
        Content = new StringContent("Hello HTTP"),
        Version = new Version(2, 0),
        VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
    });
```

Alternatively, you can force all HTTP requests to be sent with the 
required Version 2.0 property with a simple delegating HTTP handler like :

```csharp
class Http2Handler : DelegatingHandler
{
    public Http2Handler() : this(new HttpClientHandler()) { }
    public Http2Handler(HttpMessageHandler inner) : base(inner) { }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Version = new Version(2, 0);
        request.VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
        return base.SendAsync(request, cancellationToken);
    }
}
```

Which can be consumed like:

```csharp
var http = new HttpClient(new Http2Handler());

var post = await http.PostAsync("http://localhost:8081", new StringContent("Hello HTTP"));
```

Since the handler automatically sets the relevant message properties, we can use the simpler 
`Delete/Get/Post/Put` methods instead.


An example of the output during execution:

![](https://raw.githubusercontent.com/kzu/dotnet-echo/main/assets/img/echo.gif)

And running on Ubuntu:

![](https://raw.githubusercontent.com/kzu/dotnet-echo/main/assets/img/echo-ubuntu.gif)
