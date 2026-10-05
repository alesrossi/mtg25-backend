using System.Reflection;
using API.Extensions;
using API.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace UnitTests.Extensions;

public class ServiceCollectionExtensionsTests
{
    private static readonly Assembly ApiAssembly = typeof(ICardDataService).Assembly;

    // Services fail lazily on their first request when a dependency is missing,
    // so check every API constructor dependency against the registrations for
    // both card-data backends instead of relying on startup to catch it.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AddApiServices_ForCardCacheMode_RegistersEveryApiConstructorDependency(bool useRedisCache)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=test;User Id=test;Password=test;",
                ["ConnectionStrings:Redis"] = "localhost:6379",
                ["CardData:UseRedisCache"] = useRedisCache.ToString()
            })
            .Build();

        var services = new ServiceCollection();
        services
            .AddApiConfiguration(configuration)
            .AddDatabaseContexts(configuration)
            .AddCaching(configuration)
            .AddApiServices(configuration);

        var registered = services.Select(descriptor => descriptor.ServiceType).ToHashSet();

        var missing = services
            .Select(descriptor => descriptor.ImplementationType)
            .Where(type => type is not null && type.Assembly == ApiAssembly)
            .Distinct()
            .SelectMany(type => type!
                .GetConstructors()
                .OrderByDescending(ctor => ctor.GetParameters().Length)
                .First()
                .GetParameters()
                .Where(parameter => parameter.ParameterType.Assembly == ApiAssembly
                                    && !registered.Contains(parameter.ParameterType))
                .Select(parameter => $"{type!.Name} -> {parameter.ParameterType.Name}"))
            .ToList();

        missing.Should().BeEmpty();
    }
}
