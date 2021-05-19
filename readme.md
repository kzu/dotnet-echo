# dotnet-echo

This is a trivial HTTP listener program that just echoes whatever is sent to it.

Install:

```
> dotnet tool install -g dotnet-echo
```

Update:

```
> dotnet tool update -g dotnet-echo
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

![](echo.gif)
