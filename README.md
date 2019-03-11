# Expecto TestSdk integration

| Windows                                                                                                                                                                           | Mac / Linux                                                                                                                                                     |
| --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [![Build history](https://buildstats.info/appveyor/chart/YoloDev/yolodev-expecto-testsdk?branch=master)](https://ci.appveyor.com/project/YoloDev/yolodev-expecto-testsdk/history) | [![Build history](https://buildstats.info/travisci/chart/YoloDev/YoloDev.Expecto.TestSdk?branch=master)](https://travis-ci.org/YoloDev/YoloDev.Expecto.TestSdk) |


## Configuration

You can configure some of Expecto via `dotnet test`. `dotnet test` allows you to pass in `RunSettings` via the [CLI](#dotnet-test-runsettings) or using a [.runsettings](https://docs.microsoft.com/en-us/visualstudio/test/configure-unit-tests-by-using-a-dot-runsettings-file?view=vs-2017#example-runsettings-file) file. 

### dotnet test RunSettings 

From dotnet test cli help:

```
RunSettings arguments:
  Arguments to pass as RunSettings configurations. Arguments are specified as '[name]=[value]' pairs after "-- " (note the space after --).
  Use a space to separate multiple '[name]=[value]' pairs.
  See https://aka.ms/vstest-runsettings-arguments for more information on RunSettings arguments.
  Example: dotnet test -- MSTest.DeploymentEnabled=false MSTest.MapInconclusiveToFailed=True
  ```

Many of the [ExpectoConfig](https://github.com/haf/expecto#the-config) settings are settable throughusing the CLI or .runsettings file.  This test adapter has chosen to use the naming from  Expecto's [CLI arguments](https://github.com/haf/expecto#main-argv--how-to-run-console-apps) without the `--`.  You must also prefix `Expecto.` to the front of the parameter. Additionally any args that are a switch must take a boolean value.

#### RunSettings Example: 

```
dotnet test -- Expecto.parallel=false Expecto.fail-on-focused-tests=true Expecto.stress-memory-limit=120.0 
```
