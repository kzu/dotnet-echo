![Icon](assets/img/icon-32.png) dotnet-config
============

[![Version](https://img.shields.io/nuget/v/dotnet-echo.svg?color=royalblue)](https://www.nuget.org/packages/dotnet-echo)
[![Downloads](https://img.shields.io/nuget/dt/dotnet-echo.svg?color=darkmagenta)](https://www.nuget.org/packages/dotnet-echo)
[![License](https://img.shields.io/github/license/kzu/dotnet-echo.svg?color=blue)](https://github.com/kzu/dotnet-echo/blob/main/LICENSE)
[![CI Status](https://github.com/kzu/dotnet-file/workflows/build/badge.svg?branch=main)](https://github.com/kzu/dotnet-file/actions?query=branch%3Amain+workflow%3Abuild+)
[![CI Version](https://img.shields.io/endpoint?url=https://shields.kzu.io/vpre/dotnet-echo/main&label=nuget.ci&color=brightgreen)](https://pkg.kzu.io/index.json)

Installing or updating (same command can be used for both):

```
dotnet tool update -g dotnet-echo
```

Usage:

```
> dotnet echo -?
echo
  A trivial program that echoes whatever is sent to it via HTTP.

Usage:
  echo [options]

Options:
  -p, --prefix <prefix>  Prefix to listen on such as http://127.0.0.0:80/ [default: http://*:80/]
  --version              Show version information
  -?, -h, --help         Show help and usage information
```

The program will automatically check for updates once a day and recommend updating 
if there is a new version available.

An example of the output during execution:

![](assets/img/echo.gif)

And running on Ubuntu:

![](assets/img/echo-ubuntu.gif)
