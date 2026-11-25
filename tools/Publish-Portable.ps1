$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$output = Join-Path $root 'artifacts\ASM-portable-0.2.0-preview2'
Push-Location $root
try {
    & dotnet publish '.\src\ASM\ASM.csproj' -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:PublishTrimmed=false `
        -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
        -p:DebugType=None -p:DebugSymbols=false -o $output
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish terminó con código $LASTEXITCODE" }
}
finally { Pop-Location }

Write-Output "Portable 0.2.0-preview2 listo en: $output\ASM.exe"
