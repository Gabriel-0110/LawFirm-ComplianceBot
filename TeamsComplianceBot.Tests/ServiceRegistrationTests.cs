using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TeamsComplianceBot.Services;
using Xunit;

namespace TeamsComplianceBot.Tests;

public class ServiceRegistrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ServiceRegistrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public void AllRequiredServicesAreRegistered()
    {
        // Arrange & Act
        var serviceProvider = _factory.Services;

        // Assert - Verify all critical services are registered
        Assert.NotNull(serviceProvider.GetService<ICallRecordingService>());
        Assert.NotNull(serviceProvider.GetService<IComplianceService>());
        Assert.NotNull(serviceProvider.GetService<INotificationService>());
        Assert.NotNull(serviceProvider.GetService<ICallJoiningService>());
        Assert.NotNull(serviceProvider.GetService<IGraphSubscriptionService>());
    }

    [Fact]
    public void WebhookEndpointExists()
    {
        // This test verifies the webhook endpoint is properly configured
        var client = _factory.CreateClient();
        
        // The endpoint should exist (though it will return bad request without proper data)
        var response = client.GetAsync("/api/graphwebhook?validationToken=test").Result;
        
        // Should not be 404 (Not Found)
        Assert.NotEqual(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }
}
