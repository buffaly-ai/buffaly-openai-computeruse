param(
	[string]$InstallRoot = "C:\dev\Buffaly.Development\buffaly.agent.web",
	[string]$Configuration = "Debug",
	[string]$PublishRoot = "",
	[string]$ProvisioningRoot = "C:\dev\Buffaly.Provisioning"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "Buffaly.OpenAI.ComputerUse.WebHarness\Buffaly.OpenAI.ComputerUse.WebHarness.csproj"
if ([string]::IsNullOrWhiteSpace($PublishRoot)) {
	$PublishRoot = Join-Path ([System.IO.Path]::GetTempPath()) "computeruse-module-publish"
}

$provisioningSolution = Join-Path $ProvisioningRoot "Buffaly.Provisioning.sln"
$provisioningCmdProject = Join-Path $ProvisioningRoot "Buffaly.Agent.Provisioning.Cmd\Buffaly.Agent.Provisioning.Cmd.csproj"

if (!(Test-Path $projectPath)) { throw "ComputerUse web harness project not found: $projectPath" }
if (!(Test-Path $provisioningSolution)) { throw "Buffaly.Provisioning solution not found: $provisioningSolution" }
if (!(Test-Path $provisioningCmdProject)) { throw "Provisioning command project not found: $provisioningCmdProject" }
if (!(Test-Path $InstallRoot)) { throw "Install root not found: $InstallRoot" }

Write-Host "Building provisioning..."
dotnet build $provisioningSolution -m:1 | Write-Host
if ($LASTEXITCODE -ne 0) { throw "Provisioning build failed." }

if (Test-Path $PublishRoot) { Remove-Item -Recurse -Force $PublishRoot }
New-Item -ItemType Directory -Path $PublishRoot -Force | Out-Null

Write-Host "Publishing ComputerUse module to $PublishRoot ..."
dotnet publish $projectPath -c $Configuration -o $PublishRoot -m:1 | Write-Host
if ($LASTEXITCODE -ne 0) { throw "ComputerUse publish failed." }

$requiredPublishPaths = @(
	"Buffaly.OpenAI.ComputerUse.WebHarness.dll",
	"Generated\JsonWs\Buffaly.OpenAI.ComputerUse.WebHarness.ComputerUseWorkbenchJsonWsService.json",
	"Generated\JsonWs\Buffaly.OpenAI.ComputerUse.WebHarness.ComputerUseWorkbenchJsonWsService.ashx.js",
	"wwwroot\index.html",
	"Skills\ComputerUse\index.pts"
)
foreach ($relativePath in $requiredPublishPaths) {
	$fullPath = Join-Path $PublishRoot $relativePath
	if (!(Test-Path $fullPath)) { throw "Published output missing required path: $relativePath" }
}

Write-Host "Installing ComputerUse module into $InstallRoot ..."
dotnet run --project $provisioningCmdProject -- install-published-web-module $InstallRoot $PublishRoot ComputerUse | Write-Host
if ($LASTEXITCODE -ne 0) { throw "ComputerUse install failed." }

$requiredInstallPaths = @(
	"wwwroot\web-modules\ComputerUse\index.html",
	"wwwroot\web-modules\ComputerUse\styles\computer-use-workbench.css",
	"wwwroot\web-modules\ComputerUse\js\computer-use-workbench.js",
	"wwwroot\web-modules\ComputerUse\images\bf_logo_fav_Large.png",
	"wwwroot\JsonWs\Buffaly.OpenAI.ComputerUse.WebHarness.ComputerUseWorkbenchJsonWsService.json",
	"wwwroot\JsonWs\Buffaly.OpenAI.ComputerUse.WebHarness.ComputerUseWorkbenchJsonWsService.ashx.js",
	"content\projects\OpsAgent\Skills\ComputerUse\index.pts"
)
foreach ($relativePath in $requiredInstallPaths) {
	$fullPath = Join-Path $InstallRoot $relativePath
	if (!(Test-Path $fullPath)) { throw "Installed output missing required path: $relativePath" }
}

$installedIndexPath = Join-Path $InstallRoot "wwwroot\web-modules\ComputerUse\index.html"
$installedIndex = Get-Content -Raw $installedIndexPath
if ($installedIndex -notmatch 'href="styles/computer-use-workbench.css"') { throw "Installed index.html does not use relative stylesheet path." }
if ($installedIndex -notmatch 'src="images/bf_logo_fav_Large.png"') { throw "Installed index.html does not use relative image path." }
if ($installedIndex -notmatch 'src="js/computer-use-workbench.js"') { throw "Installed index.html does not use relative page script path." }

[pscustomobject]@{
	Succeeded = $true
	InstallRoot = $InstallRoot
	PublishRoot = $PublishRoot
	InstalledPage = $installedIndexPath
} | ConvertTo-Json -Depth 4 | Write-Host
