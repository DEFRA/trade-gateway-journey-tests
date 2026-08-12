using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Trade.Gateway.Api.Client.Clients;
using Trade.Gateway.Api.Client.Extensions;

namespace TradeGateway.Tests;


public sealed class TracesGatewayFactory : IDisposable
{
    private readonly ServiceProvider _serviceProvider;

    public TracesGatewayFactory()
    {
        Environment.SetEnvironmentVariable("AWS_ENDPOINT_URL", "http://localhost:4566");
        Environment.SetEnvironmentVariable("AWS_ENDPOINT_URL_STS", "http://localhost:3001/local/sts");
        Environment.SetEnvironmentVariable("AWS_EMF_ENVIRONMENT", "Local");
        Environment.SetEnvironmentVariable("AWS_REGION", "eu-west-2");
        Environment.SetEnvironmentVariable("AWS_DEFAULT_REGION", "eu-west-2");
        Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", "test");
        Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", "test");
        Environment.SetEnvironmentVariable("USE_FLOCI", "true");


        var configuration = new ConfigurationBuilder().SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false).AddEnvironmentVariables().Build();
        var services = new ServiceCollection();
        services.AddTracesGatewayApiClients(configuration)
            .WithSts()
            .WithAcceptLanguage()
            .WithLogging()
            .WithTracing(_ => Guid.NewGuid().ToString("N"));
        _serviceProvider = services.BuildServiceProvider();
    }

    public T GetRequiredService<T>() where T : notnull => _serviceProvider.GetRequiredService<T>();
    public ITracesGatewayClient TracesGatewayClient => GetRequiredService<ITracesGatewayClient>();
    public IReferenceDataClient ReferenceDataClient => GetRequiredService<IReferenceDataClient>();
    public ITracesGatewayChedClient TracesGatewayChedClient => GetRequiredService<ITracesGatewayChedClient>();
    public ITracesGatewayIntraClient TracesGatewayIntraClient => GetRequiredService<ITracesGatewayIntraClient>();
    public void Dispose() => _serviceProvider.Dispose();
}

[CollectionDefinition("Traces Gateway")]
public sealed class TracesGatewayCollection : ICollectionFixture<TracesGatewayFactory>
{
}