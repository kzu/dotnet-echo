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
  echo [options] [<endpoint>...]

Arguments:
  <endpoint>  Endpoint to listen on such as https://127.0.0.0:8080/ [default: https://127.0.0.1:4242/]

Options:
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

Since gRPC needs to use HTTP/2, it will use the defined `prefix` port + 1 (i.e. if you specify 
`http://127.0.0.1:8080`, the gRPC endpoint will be available at `http://127.0.0.1:8081`).

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


An example of the output during execution:

![](https://raw.githubusercontent.com/kzu/dotnet-echo/main/assets/img/echo.gif)

And running on Ubuntu:

![](https://raw.githubusercontent.com/kzu/dotnet-echo/main/assets/img/echo-ubuntu.gif)
