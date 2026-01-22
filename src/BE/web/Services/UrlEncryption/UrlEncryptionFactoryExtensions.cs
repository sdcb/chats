
namespace Chats.BE.Services.UrlEncryption;

public static class UrlEncryptionFactoryExtensions
{
    public static IServiceCollection AddUrlEncryption(this IServiceCollection services)
    {
        services.AddSingleton<UrlEncryptionFactory>();
        services.AddSingleton(serviceProvider =>
        {
            UrlEncryptionFactory factory = serviceProvider.GetRequiredService<UrlEncryptionFactory>();
            return factory.Create();
        });

        return services;
    }
}