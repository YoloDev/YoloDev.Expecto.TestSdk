# `test/mtp` runner launcher

This directory exists only to provide a `global.json` that opts into the
**Microsoft.Testing.Platform (MTP) mode** of `dotnet test`
(`"test": { "runner": "Microsoft.Testing.Platform" }`).

Starting with `Microsoft.Testing.Platform` v2 on the .NET 10+ SDK, the legacy
"VSTest mode" path for running MTP test apps via `dotnet test` was removed, so
the platform integration test must run through MTP mode instead.

`dotnet test` resolves the test `runner` from the **current working directory's**
`global.json`, while `YoloDev.Sdk` resolves the repository root (and therefore
`version.txt`) from the **test project's** directory. The `test-platform`
recipes in `.justfile` `cd` into this folder and point at
`../Sample.Test/Sample.Test.fsproj`, so the platform tests run under the MTP
runner while the legacy (VSTest) recipes keep running from the repo root under
the default VSTest runner.
