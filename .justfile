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
  dotnet clean ./test/Sample.Test/Sample.Test.fsproj -v:quiet
  dotnet test ./test/Sample.Test/Sample.Test.fsproj -p:EnableExpectoTestingPlatformIntegration=true -p:IncludeFailingTests=false

test-platform-failing: pack
  dotnet clean ./test/Sample.Test/Sample.Test.fsproj -v:quiet
  dotnet test ./test/Sample.Test/Sample.Test.fsproj -p:EnableExpectoTestingPlatformIntegration=true -p:IncludeFailingTests=true

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

@test: check-platform check-legacy
