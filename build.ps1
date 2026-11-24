param([string] $OutputPath, [switch] $Test)

$ErrorActionPreference = 'Stop'
$editionFlags = @()
$executable = 'DeskPlant.exe'
if ($Test) {
    $editionFlags += '/define:DESKPLANT_TEST'
    $executable = 'DeskPlant-Test.exe'
}
if (!$OutputPath) { $OutputPath = Join-Path $PSScriptRoot ('output\windows\' + $executable) }

$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (!(Test-Path -LiteralPath $compiler)) {
    $framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319'
    $compiler = Join-Path $framework 'csc.exe'
}
if (!(Test-Path -LiteralPath $compiler)) { throw 'The Windows .NET Framework compiler was not found.' }

$source = Join-Path $PSScriptRoot 'src'
$application = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPath)
$output = Split-Path -Parent $application
$assets = Join-Path $PSScriptRoot 'assets'
$resources = @(
    "/resource:$(Join-Path $assets 'jade-seedling.png'),DeskPlant.JadeSeedling"
    "/resource:$(Join-Path $assets 'jade-young.png'),DeskPlant.JadeYoung"
    "/resource:$(Join-Path $assets 'jade-plant.png'),DeskPlant.JadePlant"
    "/resource:$(Join-Path $assets 'snake-plant-stages.png'),DeskPlant.SnakeStages"
    "/resource:$(Join-Path $assets 'purple-oxalis-stages.png'),DeskPlant.OxalisStages"
    "/resource:$(Join-Path $assets 'aloe-stages.png'),DeskPlant.AloeStages"
    "/resource:$(Join-Path $assets 'lavender-stages.png'),DeskPlant.LavenderStages"
    "/resource:$(Join-Path $assets 'mini-rose-stages.png'),DeskPlant.MiniRoseStages"
    "/resource:$(Join-Path $assets 'watering-can.png'),DeskPlant.WateringCan"
    "/resource:$(Join-Path $assets 'shop\porcelain.png'),DeskPlant.Shop.Porcelain"
    "/resource:$(Join-Path $assets 'shop\clay.png'),DeskPlant.Shop.Clay"
    "/resource:$(Join-Path $assets 'shop\brass.png'),DeskPlant.Shop.Brass"
    "/resource:$(Join-Path $assets 'shop\mint.png'),DeskPlant.Shop.Mint"
    "/resource:$(Join-Path $assets 'fruit-plants\layers.json'),DeskPlant.Crop.Layers"
    "/resource:$(Join-Path $assets 'fruit-plants\warehouse-fruit-atlas.png'),DeskPlant.WarehouseFruit"
    "/resource:$(Join-Path $assets 'spaces\garden.jpg'),DeskPlant.Space.Garden"
    "/resource:$(Join-Path $assets 'spaces\orchard.jpg'),DeskPlant.Space.Orchard"
    "/resource:$(Join-Path $assets 'spaces\warehouse.jpg'),DeskPlant.Space.Warehouse"
    "/resource:$(Join-Path $assets 'spaces\garden-bed.png'),DeskPlant.Space.GardenBed"
    "/resource:$(Join-Path $assets 'spaces\orchard-bed.png'),DeskPlant.Space.OrchardBed"
)
foreach ($crop in @('passion-fruit','pineapple','blueberry','strawberry','kumquat','cherry-tomato')) {
    $resources += "/resource:$(Join-Path $assets ('fruit-plants\' + $crop + '-stages.png')),DeskPlant.Crop.$crop.Stages"
    $resources += "/resource:$(Join-Path $assets ('fruit-plants\' + $crop + '-parts.png')),DeskPlant.Crop.$crop.Parts"
}
$manifest = Join-Path $source 'DeskPlant.manifest'
$sources = @(Get-ChildItem -LiteralPath $source -File -Filter '*.cs' | Select-Object -ExpandProperty FullName)
New-Item -ItemType Directory -Force -Path $output | Out-Null

& $compiler /nologo /target:winexe /optimize+ /codepage:65001 /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll "/win32manifest:$manifest" "/out:$application" @editionFlags @resources @sources
if ($LASTEXITCODE -ne 0) { throw "DeskPlant build failed ($LASTEXITCODE)." }
Write-Output $application
