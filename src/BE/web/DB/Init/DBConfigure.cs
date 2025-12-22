using Microsoft.EntityFrameworkCore;

namespace Chats.Web.DB.Init;

public static class DBConfigure
{
    public static void Configure(this DbContextOptionsBuilder dbContextOptionsBuilder, IConfiguration configuration, IHostEnvironment environment)
    {
        string? dbType = configuration["DBType"];
        string? connectionString = configuration.GetConnectionString("ChatsDB") ?? throw new Exception("ConnectionStrings:ChatsDB not found");
        if (dbType == null || dbType.Equals("sqlite", StringComparison.OrdinalIgnoreCase))
        {
            // help client create the AppData folder for better startup experience
            if (connectionString == "Data Source=./AppData/chats.db" && !Directory.Exists("AppData"))
            {
                Console.WriteLine("Creating AppData folder...");
                Directory.CreateDirectory("AppData");
            }
            dbContextOptionsBuilder.UseSqlite(connectionString);
        }
        else if (dbType.Equals("sqlserver", StringComparison.OrdinalIgnoreCase) || dbType.Equals("mssql", StringComparison.OrdinalIgnoreCase))
        {
            dbContextOptionsBuilder.UseSqlServer(connectionString);
        }
        else if (dbType.Equals("postgresql", StringComparison.OrdinalIgnoreCase) || dbType.Equals("pgsql", StringComparison.OrdinalIgnoreCase))
        {
            dbContextOptionsBuilder.UseNpgsql(connectionString);
        }
        else
        {
            throw new Exception("Unknown DBType: " + dbType);
        }

        if (environment.IsDevelopment())
        {
            dbContextOptionsBuilder.EnableSensitiveDataLogging();
        }
    }
}
