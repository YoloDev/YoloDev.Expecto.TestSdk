[private]
@list:
  just --list

restore:
  dotnet restore -p:Configuration=Release

build: restore
  dotnet build --configuration Release --no-restore

pack: build
  dotnet pack --configuration Release --no-build

clean:
  dotnet clean

test-platform: pack
  cd test/mtp && dotnet clean ../Sample.Test/Sample.Test.fsproj -v:quiet
  cd test/mtp && dotnet test --project ../Sample.Test/Sample.Test.fsproj -p:EnableExpectoTestingPlatformIntegration=true -p:IncludeFailingTests=false

test-platform-failing: pack
  cd test/mtp && dotnet clean ../Sample.Test/Sample.Test.fsproj -v:quiet
  cd test/mtp && dotnet test --project ../Sample.Test/Sample.Test.fsproj -p:EnableExpectoTestingPlatformIntegration=true -p:IncludeFailingTests=true

# Unit tests for the adapter, plus end-to-end tests that drive the platform sample over the IDE (server-mode) protocol
test-adapter: pack
  cd test/mtp && dotnet build ../Sample.Test/Sample.Test.fsproj -p:EnableExpectoTestingPlatformIntegration=true -p:IncludeFailingTests=false
  EXPECTO_SAMPLE_TEST_APP="{{justfile_directory()}}/test/Sample.Test/bin/Debug/net8.0/Sample.Test.dll" dotnet run --project test/YoloDev.Expecto.TestSdk.Tests --configuration Release

test-legacy: pack
  dotnet clean ./test/Sample.Test/Sample.Test.fsproj -v:quiet
  dotnet test ./test/Sample.Test/Sample.Test.fsproj -p:EnableExpectoTestingPlatformIntegration=false  -p:IncludeFailingTests=false

test-legacy-failing: pack
  dotnet clean ./test/Sample.Test/Sample.Test.fsproj -v:quiet
  dotnet test ./test/Sample.Test/Sample.Test.fsproj -p:EnableExpectoTestingPlatformIntegration=false  -p:IncludeFailingTests=true

@check-platform:
  #!/usr/bin/env bash
  just test-platform
  status=$?

  if [ $status -ne 0 ]; then
    echo "Expected tests to pass, but they failed (status code: $status)"
    exit 1
  fi

  just test-platform-failing
  status=$?

  if [ $status -eq 0 ]; then
    echo "Expected tests to fail, but they passed (status code: $status)"
    exit 1
  fi

  echo "Test run worked as expected"

@check-legacy:
  #!/usr/bin/env bash
  just test-legacy
  status=$?

  if [ $status -ne 0 ]; then
    echo "Expected tests to pass, but they failed (status code: $status)"
    exit 1
  fi

  just test-legacy-failing
  status=$?

  if [ $status -eq 0 ]; then
    echo "Expected tests to fail, but they passed (status code: $status)"
    exit 1
  fi

  echo "Test run worked as expected"

@test: check-platform check-legacy test-adapter
