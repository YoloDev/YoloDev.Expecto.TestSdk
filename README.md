# Expecto TestSdk integration

![CI](https://github.com/YoloDev/YoloDev.Expecto.TestSdk/workflows/CI/badge.svg?event=push)
[![NuGet Status](http://img.shields.io/nuget/v/YoloDev.Expecto.TestSdk.svg?style=flat)](https://www.nuget.org/packages/YoloDev.Expecto.TestSdk)

## Using Expecto with VSTest (dotnet test)

To use as the Expecto test adapter, add the following dependencies to your project:

```
Microsoft.NET.Test.Sdk
YoloDev.Expecto.TestSdk
```

If you're using the dotnet CLI, and it's built in package management, the following
commands can be used to achive that. References can also be added using visual studio
NuGet browser, or paket.

```shell
dotnet add package Microsoft.NET.Test.Sdk
dotnet add package YoloDev.Expecto.TestSdk
```

In addition, it might be nesesary to disable the automatic generation of
a `program.fs` file by msbuild, depending on your target framework. To do
so, set `GenerateProgramFile` to false in the `fsproj` file, as seen bellow:

```xml
<PropertyGroup>
  <GenerateProgramFile>false</GenerateProgramFile>
</PropertyGroup>
```

To get the tests working in the Visual Studio test explorer, it's recommended to target
`netcoreapp2.2` or newer with your test projects. Others _might_ work, but people have
had problems with them.

## Configuration

You can configure some of Expecto via `dotnet test`. `dotnet test` allows you to pass in `RunSettings` via the [CLI](#dotnet-test-runsettings) or using a [.runsettings](https://docs.microsoft.com/en-us/visualstudio/test/configure-unit-tests-by-using-a-dot-runsettings-file?view=vs-2017#example-runsettings-file) file.

### dotnet test RunSettings

From dotnet test cli help:

> RunSettings arguments:
> Arguments to pass as RunSettings configurations. Arguments are specified as '[name]=[value]' pairs after "-- " (note the space after --).
> Use a space to separate multiple '[name]=[value]' pairs.
> See https://aka.ms/vstest-runsettings-arguments for more information on RunSettings arguments.
> Example: dotnet test -- MSTest.DeploymentEnabled=false MSTest.MapInconclusiveToFailed=True

Many of the [ExpectoConfig](https://github.com/haf/expecto#the-config) settings are settable throughusing the CLI or .runsettings file. This test adapter uses the naming from Expecto's [CLI arguments](https://github.com/haf/expecto#main-argv--how-to-run-console-apps) (without the leading `--`), namespaced with `Expecto.`. Additionally, any args that are switches must take a boolean value.

#### RunSettings Example:

```
dotnet test -- Expecto.parallel=false Expecto.fail-on-focused-tests=true Expecto.stress-memory-limit=120.0
```
