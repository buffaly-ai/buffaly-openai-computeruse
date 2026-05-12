using Buffaly.OpenAI.ComputerUse.WebHarness;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
	options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

var module = new ComputerUseWorkbenchModule();
module.Configure(app.Environment.ContentRootPath, app.Environment.WebRootPath ?? string.Empty);
module.MapRoutes(app);

app.Run();