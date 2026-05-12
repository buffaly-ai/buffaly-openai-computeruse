using Buffaly.Agent.Web.Common;
using Microsoft.AspNetCore.Builder;
using WebAppUtilities;

namespace Buffaly.OpenAI.ComputerUse.WebHarness;

public sealed class ComputerUseWorkbenchModule : IBuffalyWebModule
{
	public string ModuleName => "ComputerUse";

	// Capture the host paths at startup. The current module relies on JsonWs artifacts and static files installed by the module installer.
	public void Configure(string contentRootPath, string webRootPath)
	{
		if (string.IsNullOrWhiteSpace(contentRootPath))
		{
			throw new ArgumentException("contentRootPath is required.", nameof(contentRootPath));
		}
		if (string.IsNullOrWhiteSpace(webRootPath))
		{
			throw new ArgumentException("webRootPath is required.", nameof(webRootPath));
		}
	}

	// Legacy contract method retained for IBuffalyWebModule compatibility; new installs copy publish artifacts by convention.
	public void InstallArtifacts(string contentRootPath, string webRootPath)
	{
		if (string.IsNullOrWhiteSpace(contentRootPath))
		{
			throw new ArgumentException("contentRootPath is required.", nameof(contentRootPath));
		}
		if (string.IsNullOrWhiteSpace(webRootPath))
		{
			throw new ArgumentException("webRootPath is required.", nameof(webRootPath));
		}
	}

	// Register JsonWs endpoints and the run-file route used by the computer-use workbench page.
	public void MapRoutes(WebApplication app)
	{
		JsonWsOptions jsonWsOptions = new();
		Buffaly.Common.JsonWsHandlerService.RegisterApis(app, jsonWsOptions, new[] { "*.json" });

		ComputerUseWorkbenchRuntime runtime = ComputerUseWorkbenchJsonWsService.GetRuntime();
		app.MapGet("/computer-use/runfiles/{runId}/{*relativePath}", (string runId, string relativePath) =>
		{
			string? filePath = runtime.ResolveRunFile(runId, relativePath);
			if (filePath == null)
			{
				return Results.NotFound();
			}

			return Results.File(filePath, runtime.GetContentType(filePath));
		});
	}
}