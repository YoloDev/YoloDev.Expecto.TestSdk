# create this function in the calling script
function Get-ScriptDirectory { Split-Path $MyInvocation.ScriptName }

function __exec($cmd) {
  $cmdName = [IO.Path]::GetFileName($cmd)
  Write-Host -ForegroundColor Cyan "> $cmdName $args"
  $originalErrorPref = $ErrorActionPreference
  $ErrorActionPreference = 'Continue'
  & $cmd @args
  $exitCode = $LASTEXITCODE
  $ErrorActionPreference = $originalErrorPref
  if ($exitCode -ne 0) {
    throw "'$cmdName $args' failed with exit code: $exitCode"
  }
}

$bindir = Join-Path (Get-ScriptDirectory) "../bin"
if (Test-Path $bindir) {
  Remove-Item -Recurse -Force $bindir
}

$projects = Get-ChildItem src/**/*.*proj
foreach ($project in $projects) {
  __exec dotnet pack "$project" "-c" "release" "-o" "$bindir" "/v:m" "/p:ci=true"
}

$nupkgs = Get-ChildItem bin/*.nupkg
Push-Location "build"
try {
  __exec dotnet restore
  foreach ($nupkg in $nupkgs) {
    __exec dotnet sourcelink test $nupkg
  }
}
finally {
  Pop-Location
}