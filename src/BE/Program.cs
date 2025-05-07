using Chats.BE.DB;
using Chats.BE.DB.Init;
using Chats.BE.Infrastructure;
using Chats.BE.Services;
using Chats.BE.Services.Configs;
using Chats.BE.Services.Models;
using Chats.BE.Services.UrlEncryption;
using Chats.BE.Services.OpenAIApiKeySession;
using Chats.BE.Services.Sessions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Runtime.CompilerServices;
using Chats.BE.Services.FileServices;
using Microsoft.AspNetCore.StaticFiles;
using Chats.BE.Services.Models.ChatServices;

[assembly: InternalsVisibleTo("Chats.BE.Tests")]

namespace Chats.BE;

public class Program
{
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
        builder.Services.AddHttpClient();
        builder.Services.AddSingleton<InitService>();
        builder.Services.AddSingleton<AppConfigService>();
        builder.Services.AddSingleton<CsrfTokenService>();
        builder.Services.AddSingleton<JwtKeyManager>();
        builder.Services.AddSingleton<PasswordHasher>();
        builder.Services.AddSingleton<HostUrlService>();
        builder.Services.AddSingleton<ChatFactory>();
        builder.Services.AddSingleton<BalanceService>();
        builder.Services.AddSingleton<FileServiceFactory>();
        builder.Services.AddSingleton<ChatStopService>();
        builder.Services.AddSingleton<FileImageInfoService>();
        builder.Services.AddSingleton<AsyncClientInfoManager>();

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
        builder.Services.AddScoped<FileContentTypeService>();
        builder.Services.AddScoped<DBFileService>();

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
