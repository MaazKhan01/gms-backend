namespace Host.Configurations
{
    public static class Startup
    {
        public static WebApplicationBuilder AddConfigurations(this WebApplicationBuilder builder)
        {
            const string configurationsDirectory = "Configurations";
            var env = builder.Environment;
            builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
                    .AddJsonFile($"{configurationsDirectory}/logger.json", optional: false, reloadOnChange: true)
                    .AddJsonFile($"{configurationsDirectory}/auth.json", optional: false, reloadOnChange: true)
                    .AddJsonFile($"{configurationsDirectory}/cache.json", optional: false, reloadOnChange: true)
                    .AddJsonFile($"{configurationsDirectory}/database.json", optional: false, reloadOnChange: true)
                    .AddEnvironmentVariables();
            return builder;
        }
    }
}
