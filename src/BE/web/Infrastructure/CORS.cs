namespace Chats.Web.Infrastructure;

internal static class CORS
{
    public static void AddCORSPolicies(this WebApplicationBuilder builder)
    {
        string frontendUrl = builder.Configuration.GetValue<string>("FE_URL") ?? throw new ArgumentNullException("FE_URL is required in the configuration");

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("FrontendCORS", policy =>
            {
                policy.WithOrigins(frontendUrl, "http://localhost:3000")
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials()
                      .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
            });
        });
    }

    public static void UseCORSMiddleware(this WebApplication app)
    {
        app.UseCors("FrontendCORS");
    }
}
