using Chats.Capture.Browser;
using Chats.Capture.Configuration;
using Chats.Capture.Infrastructure;
using Chats.Capture.Output;
using Chats.Capture.Services;
using Chats.Capture.State;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var runOptions = CliParser.Parse(args);
if (runOptions.ShowHelp)
{
	CliParser.WriteHelp();
	return 0;
}

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration
	.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
	.AddEnvironmentVariables();

var settings = CaptureSettings.FromConfiguration(builder.Configuration);

builder.Services.AddSingleton(settings);
builder.Services.AddSingleton(runOptions);
builder.Services.AddSingleton<CaptureOutputService>();
builder.Services.AddSingleton<PlaywrightBrowserService>();
builder.Services.AddSingleton<StatePreparationService>();
builder.Services.AddSingleton<CaptureRunner>();
builder.Services.AddSingleton<CaptureApplication>();

using var host = builder.Build();
var application = host.Services.GetRequiredService<CaptureApplication>();
return await application.RunAsync(CancellationToken.None);
