[private]
@list:
  just --list

restore:
  dotnet restore -p:Configuration=Release

build: restore
  dotnet build --configuration Release --no-restore

pack: build
  dotnet pack --configuration Release --no-build

test-platform: pack
  dotnet clean ./test/Sample.Test/Sample.Test.fsproj -v:quiet
  dotnet test ./test/Sample.Test/Sample.Test.fsproj -p:EnableExpectoRunner=true -p:IncludeFailingTests=false

test-platform-failing: pack
  dotnet clean ./test/Sample.Test/Sample.Test.fsproj -v:quiet
  dotnet test ./test/Sample.Test/Sample.Test.fsproj -p:EnableExpectoRunner=true -p:IncludeFailingTests=true

test-legacy: pack
  dotnet clean ./test/Sample.Test/Sample.Test.fsproj -v:quiet
  dotnet test ./test/Sample.Test/Sample.Test.fsproj -p:EnableExpectoRunner=false  -p:IncludeFailingTests=false

test-legacy-failing: pack
  dotnet clean ./test/Sample.Test/Sample.Test.fsproj -v:quiet
  dotnet test ./test/Sample.Test/Sample.Test.fsproj -p:EnableExpectoRunner=false  -p:IncludeFailingTests=true

@test: test-platform test-legacy
