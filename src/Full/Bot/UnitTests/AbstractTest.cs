

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace UnitTests;

public abstract class AbstractTest
{
    protected ILogger _logger;
    protected TestsConfig _config;

    public AbstractTest()
    {
        _logger = LoggerFactory.Create(config =>
        {
            config.AddConsole();
        }).CreateLogger("Tests");

        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddUserSecrets(System.Reflection.Assembly.GetExecutingAssembly())
            .AddJsonFile("appsettings.json", true);

        var configCollection = builder.Build();
        _config = new TestsConfig(configCollection);

    }

    protected ILogger<T> GetLogger<T>()
    {
        return LoggerFactory.Create(config =>
        {
            config.AddConsole();
        }).CreateLogger<T>();
    }
}
