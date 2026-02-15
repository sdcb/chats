using Chats.DB;
using Chats.BE.DB.Init;
using Chats.BE.Infrastructure;
using Chats.BE.Services;
using Chats.BE.Services.Configs;
using Chats.BE.Services.Models;
using Chats.BE.Services.UrlEncryption;
using Chats.BE.Services.OpenAIApiKeySession;
using Chats.BE.Services.Sessions;
using Chats.BE.Services.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;
using Chats.BE.Services.FileServices;
using Microsoft.AspNetCore.StaticFiles;
using Chats.BE.Services.Models.ChatServices;
using Chats.BE.Controllers.Admin.GlobalConfigs;
using Chats.BE.Services.CodeInterpreter;
using Chats.BE.Services.Options;
using Chats.DockerInterface;
using Microsoft.Extensions.Options;
using Chats.BE.Services.Keycloak;

namespace Chats.BE;

public class Program
{
    private static string? CurrentVersion => typeof(Program).Assembly
        .GetCustomAttribute<AssemblyFileVersionAttribute>()?
        .Version;

    public static async Task Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddControllers(options =>
        {
            options.CacheProfiles.Add("ModelInfo", new CacheProfile()
            {
                Duration = 5 * 60,
                Location = ResponseCacheLocation.Client,
            });
        });
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddDbContext<ChatsDB>(o => o.Configure(builder.Configuration, builder.Environment));
        builder.Services.AddHttpClient(string.Empty, httpClient =>
        {
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"Sdcb-Chats/{CurrentVersion}");
        });

        builder.Services.AddSingleton<KeycloakOAuthClient>();
        builder.Services.AddSingleton<InitService>();
        builder.Services.AddSingleton<AppConfigService>();
        builder.Services.AddSingleton<CsrfTokenService>();
        builder.Services.AddSingleton<JwtKeyManager>();
        builder.Services.AddSingleton<PasswordHasher>();
        builder.Services.AddSingleton<HostUrlService>();
        builder.Services.AddSingleton<ChatFactory>();
        builder.Services.AddSingleton<Services.Models.ChatServices.Test.Test2ChatService>();
        builder.Services.AddSingleton<Services.Models.ChatServices.OpenAI.ChatCompletionService>();
        builder.Services.AddSingleton<Services.Models.ChatServices.OpenAI.DeepSeekChatService>();
        builder.Services.AddSingleton<Services.Models.ChatServices.OpenAI.AzureAIFoundryChatService>();
        builder.Services.AddSingleton<Services.Models.ChatServices.OpenAI.QianFan.QianFanChatService>();
        builder.Services.AddSingleton<Services.Models.ChatServices.OpenAI.QwenChatService>();
        builder.Services.AddSingleton<Services.Models.ChatServices.OpenAI.GLMChatService>();
        builder.Services.AddSingleton<Services.Models.ChatServices.OpenAI.HunyuanChatService>();
        builder.Services.AddSingleton<Services.Models.ChatServices.OpenAI.LingYiChatService>();
        builder.Services.AddSingleton<Services.Models.ChatServices.OpenAI.XAIChatService>();
        builder.Services.AddSingleton<Services.Models.ChatServices.OpenAI.GithubModelsChatService>();
        builder.Services.AddSingleton<Services.Models.ChatServices.GoogleAI.GoogleAI2ChatService>();
        builder.Services.AddSingleton<Services.Models.ChatServices.OpenAI.SiliconFlowChatService>();
        builder.Services.AddSingleton<Services.Models.ChatServices.OpenAI.TokenPonyChatService>();
        builder.Services.AddSingleton<Services.Models.ChatServices.OpenAI.OpenRouterChatService>();
        builder.Services.AddSingleton<Services.Models.ChatServices.OpenAI.Special.ResponseApiService>();
        builder.Services.AddSingleton<Services.Models.ChatServices.OpenAI.Special.AzureResponseApiService>();
        builder.Services.AddSingleton<Services.Models.ChatServices.OpenAI.MiniMaxChatService>();
        builder.Services.AddSingleton<Services.Models.ChatServices.Anthropic.AnthropicChatService>();
        builder.Services.AddSingleton<Services.Models.ChatServices.Anthropic.DeepSeekAnthropicService>();
        builder.Services.AddSingleton<Services.Models.ChatServices.Anthropic.MiniMaxAnthropicService>();
        builder.Services.AddSingleton<Services.Models.ChatServices.Anthropic.MimoAnthropicService>();
        builder.Services.AddSingleton<Services.Models.ChatServices.OpenAI.MimoChatService>();
        builder.Services.AddSingleton<Services.Models.ChatServices.OpenAI.MoonshotChatService>();

        builder.Services.AddSingleton<Services.Models.ChatServices.OpenAI.Special.ImageGenerationService>();
        builder.Services.AddSingleton<Services.Models.ChatServices.OpenAI.Special.AzureImageGenerationService>();
        builder.Services.AddSingleton<BalanceService>();
        builder.Services.AddSingleton<IFileServiceFactory, FileServiceFactory>();
        builder.Services.AddSingleton<ChatStopService>();
        builder.Services.AddSingleton<FileImageInfoService>();
        builder.Services.AddSingleton<AsyncClientInfoManager>();
        builder.Services.AddSingleton<AsyncCacheUsageManager>();
        builder.Services.AddSingleton<GitHubReleaseChecker>();

        builder.Services.AddScoped<CurrentUser>();
        builder.Services.AddScoped<CurrentApiKey>();
        builder.Services.AddScoped<GlobalDBConfig>();
        builder.Services.AddScoped<UserManager>();
        builder.Services.AddScoped<SessionManager>();
        builder.Services.AddScoped<UserModelManager>();
        builder.Services.AddScoped<OpenAIApiKeySessionManager>();
        builder.Services.AddScoped<ClientInfoManager>();
        builder.Services.AddScoped<FileUrlProvider>();
        builder.Services.AddScoped<ChatConfigService>();
        builder.Services.AddScoped<DBFileService>();
        builder.Services.AddScoped<LoginRateLimiter>();

        builder.Services.Configure<CodePodConfig>(builder.Configuration.GetSection("CodePod"));
        builder.Services.AddSingleton<IDockerService>(sp =>
            new DockerService(
                sp.GetRequiredService<IOptions<CodePodConfig>>().Value,
                sp.GetService<ILogger<DockerService>>()));

        builder.Services.AddSingleton<IValidateOptions<CodeInterpreterOptions>, CodeInterpreterOptionsValidator>();
        builder.Services.AddOptions<CodeInterpreterOptions>()
            .Bind(builder.Configuration.GetSection("CodeInterpreter"))
            .ValidateOnStart();
        builder.Services.AddScoped<CodeInterpreterExecutor>();
        builder.Services.AddHostedService<ChatDockerSessionCleanupService>();
        builder.Services.Configure<ChatOptions>(builder.Configuration.GetSection("Chat"));

        builder.Services.AddUrlEncryption();
        builder.Services.AddHttpContextAccessor();

        // Add authentication and configure the default scheme
        builder.Services.AddAuthentication("SessionId")
            .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>("SessionId", null)
            .AddScheme<AuthenticationSchemeOptions, OpenAIApiKeyAuthenticationHandler>("OpenAIApiKey", null);

        builder.AddCORSPolicies();

        WebApplication app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseCORSMiddleware();

        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.UseMiddleware<FrontendMiddleware>();
        app.UseStaticFiles();
        app.UseStaticFiles(new StaticFileOptions()
        {
            ContentTypeProvider = new FileExtensionContentTypeProvider(new Dictionary<string, string>
            {
                [".avif"] = "image/avif",
            })
        });

        // before run:
        await app.Services.GetRequiredService<InitService>().Init();

        await app.RunAsync();
    }
}
