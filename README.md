# Expecto TestSdk integration

| Windows                                                                                                                                                                           | Mac / Linux                                                                                                                                                     |
| --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [![Build history](https://buildstats.info/appveyor/chart/YoloDev/yolodev-expecto-testsdk?branch=master)](https://ci.appveyor.com/project/YoloDev/yolodev-expecto-testsdk/history) | [![Build history](https://buildstats.info/travisci/chart/YoloDev/YoloDev.Expecto.TestSdk?branch=master)](https://travis-ci.org/YoloDev/YoloDev.Expecto.TestSdk) |


## Configuration

You can configure some of Expecto via `dotnet test`. `dotnet test` allows you to pass in `RunSettings` via the [CLI](#dotnet-test-runsettings) or using a [.runsettings](https://docs.microsoft.com/en-us/visualstudio/test/configure-unit-tests-by-using-a-dot-runsettings-file?view=vs-2017#example-runsettings-file) file. 

### dotnet test RunSettings 

From dotnet test cli help:

> RunSettings arguments:
>   Arguments to pass as RunSettings configurations. Arguments are specified as '[name]=[value]' pairs after "-- " (note the space after --).
>   Use a space to separate multiple '[name]=[value]' pairs.
>   See https://aka.ms/vstest-runsettings-arguments for more information on RunSettings arguments.
>   Example: dotnet test -- MSTest.DeploymentEnabled=false MSTest.MapInconclusiveToFailed=True


Many of the [ExpectoConfig](https://github.com/haf/expecto#the-config) settings are settable throughusing the CLI or .runsettings file.  This test adapter uses the naming from  Expecto's [CLI arguments](https://github.com/haf/expecto#main-argv--how-to-run-console-apps) (without the leading `--`), namespaced with `Expecto.`. Additionally, any args that are switches must take a boolean value.

#### RunSettings Example: 

```
dotnet test -- Expecto.parallel=false Expecto.fail-on-focused-tests=true Expecto.stress-memory-limit=120.0 
```

#### Visual Studio test adapter

To use as the Expecto test adapter, add the following to the paket.dependencies file

```
nuget Microsoft.NET.Test.Sdk 16.4.0
nuget YoloDev.Expecto.TestSdk
```

And include in the unit test project's paket.references file.

For native nuget rather than paket implementation use the equivalent ```<PackageReference>``` entries.

The unit test project must target netcoreapp2.2 (net472 was tested, but does not work. No other frameworks have been tested.)

Add 

```
<GenerateProgramFile>false</GenerateProgramFile>
```

to the unit test project PropertyGroup.
