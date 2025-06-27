using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Extensions.Azure;
using Microsoft.Graph;
using TeamsComplianceBot.Bots;
using TeamsComplianceBot.Services;
using TeamsComplianceBot.Middleware;
using System.Globalization;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel for Azure App Service and Bot Framework
builder.WebHost.ConfigureKestrel(options =>
{
    // Don't add Server header
    options.AddServerHeader = false;
    
    // Configure request limits for Bot Framework messages
    options.Limits.MaxRequestBodySize = 10_485_760; // 10MB for large Bot Framework payloads
    options.Limits.MaxRequestHeaderCount = 100;
    options.Limits.MaxRequestHeadersTotalSize = 32_768; // 32KB
    options.Limits.MaxRequestLineSize = 8192; // 8KB
    
    // Configure for Azure App Service environment
    if (!builder.Environment.IsDevelopment())
    {
        // Listen on the port provided by Azure App Service
        var port = Environment.GetEnvironmentVariable("PORT");
        if (!string.IsNullOrEmpty(port) && int.TryParse(port, out int portNumber))
        {
            options.ListenAnyIP(portNumber);
        }
    }
});

// CRITICAL: Set up culture safety BEFORE anything else to prevent CultureNotFoundException
// This addresses the corrupted culture identifiers issue
try
{
    // Force invariant culture to prevent culture-related crashes
    CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
    CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
    
    // Set current thread culture explicitly
    Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
    Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
    
    // Set app domain culture settings
    if (AppDomain.CurrentDomain != null)
    {
        AppDomain.CurrentDomain.SetData("APP_CULTURE", CultureInfo.InvariantCulture);
    }
    
    Console.WriteLine("Culture safety configuration applied successfully");
}
catch (Exception ex)
{
    Console.WriteLine($"CRITICAL: Error setting up culture safety: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
    // Don't let the application start if we can't set culture safety
    Environment.Exit(1);
}

// Add global exception handler for culture exceptions
var currentDomain = AppDomain.CurrentDomain;
if (currentDomain != null)
{
    currentDomain.UnhandledException += (sender, e) =>
    {
        if (e.ExceptionObject is CultureNotFoundException cultureEx)
        {
            Console.WriteLine($"CRITICAL: CultureNotFoundException caught globally: {cultureEx.Message}");
            Console.WriteLine($"Problematic culture: {cultureEx.InvalidCultureName}");
            Console.WriteLine($"Stack trace: {cultureEx.StackTrace}");
        }
        else if (e.ExceptionObject is Exception ex)
        {
            Console.WriteLine($"CRITICAL: Unhandled exception: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    };
}

// Add special configuration source for Azure App Service environment variables
// This maps from "__" format to ":" format
builder.Configuration.AddEnvironmentVariables().Build(); // Restored .Build()

// Add services to the container.
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        // Enhanced model validation error handling for better diagnostics
        options.InvalidModelStateResponseFactory = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            var correlationId = context.HttpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? Guid.NewGuid().ToString();
            
            var errors = context.ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray()
                );

            logger.LogWarning("Model validation failed for request with correlation ID {CorrelationId}. Errors: {@ValidationErrors}", 
                correlationId, errors);

            var problemDetails = new ValidationProblemDetails(context.ModelState)
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                Title = "One or more validation errors occurred.",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Please refer to the errors property for additional details.",
                Instance = context.HttpContext.Request.Path
            };

            problemDetails.Extensions.Add("correlationId", correlationId);
            problemDetails.Extensions.Add("traceId", Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);

            return new BadRequestObjectResult(problemDetails)
            {
                ContentTypes = { "application/problem+json" }
            };
        };
    });

// Configure CORS for API endpoints
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowTeamsAndAzure", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader()
              .WithExposedHeaders(new[] { "X-Correlation-ID" });
    });
});

// Add comprehensive error handling
builder.Services.AddProblemDetails();

// Add localization services to prevent culture errors
builder.Services.AddLocalization();
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[] { "en-US", "en" };
    options.SetDefaultCulture(supportedCultures[0])
           .AddSupportedCultures(supportedCultures)
           .AddSupportedUICultures(supportedCultures);
    
    // Use custom culture provider to handle invalid culture identifiers
    options.RequestCultureProviders.Clear();
    options.RequestCultureProviders.Add(new Microsoft.AspNetCore.Localization.CookieRequestCultureProvider());
});

// Handle BlobStorage1 to BlobStorage mapping for backward compatibility
var blobStorageConnectionString = builder.Configuration.GetConnectionString("BlobStorage");
var blobStorage1ConnectionString = builder.Configuration.GetConnectionString("BlobStorage1");

if (string.IsNullOrEmpty(blobStorageConnectionString) && !string.IsNullOrEmpty(blobStorage1ConnectionString))
{
    Console.WriteLine("BlobStorage connection string is missing, and BlobStorage1 is present. Mapping BlobStorage1 to BlobStorage.");
    // Correctly add BlobStorage1 as BlobStorage using an in-memory collection.
    // This ensures the configuration is properly updated.
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        { "ConnectionStrings:BlobStorage", blobStorage1ConnectionString }
    });
    Console.WriteLine("Mapped BlobStorage1 connection string to BlobStorage using in-memory configuration provider.");
}

// Add logging with Application Insights
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddApplicationInsights();
});

// Configure Application Insights telemetry with explicit connection string
// Prioritize ApplicationInsights:ConnectionString (often set via ApplicationInsights__ConnectionString env var)
// Fallback to ConnectionStrings:ApplicationInsights
var applicationInsightsConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"] 
    ?? builder.Configuration.GetConnectionString("ApplicationInsights");

if (!string.IsNullOrEmpty(applicationInsightsConnectionString))
{
    builder.Services.AddApplicationInsightsTelemetry(options =>
    {
        options.ConnectionString = applicationInsightsConnectionString;
    });
    
    // Use safe substring for logging
    try
    {
        Console.WriteLine($"Application Insights configured with connection string: {applicationInsightsConnectionString.Substring(0, Math.Min(applicationInsightsConnectionString.Length, 50))}...");
    }
    catch
    {
        Console.WriteLine("Application Insights configured with connection string (unable to display partial string)");
    }
}
else
{
    // Fallback configuration
    builder.Services.AddApplicationInsightsTelemetry(builder.Configuration);
    Console.WriteLine("Application Insights configured with default configuration");
}

// Add memory cache for caching services
builder.Services.AddMemoryCache();

// Enhanced Bot Framework configuration with authentication debugging
builder.Services.AddSingleton<BotFrameworkAuthentication>(serviceProvider =>
{
    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    
    // Log configuration for debugging
    var appId = configuration["MicrosoftAppId"];
    var appPassword = configuration["MicrosoftAppPassword"];
    var appType = configuration["MicrosoftAppType"];
    var tenantId = configuration["MicrosoftAppTenantId"];
    
    logger.LogInformation("Bot Framework Authentication Configuration:");
    logger.LogInformation("  - MicrosoftAppId: {AppId}", !string.IsNullOrEmpty(appId) ? $"{appId[..8]}..." : "NOT_SET");
    logger.LogInformation("  - MicrosoftAppPassword: {PasswordSet}", !string.IsNullOrEmpty(appPassword) ? "SET" : "NOT_SET");
    logger.LogInformation("  - MicrosoftAppType: {AppType}", appType ?? "NOT_SET");
    logger.LogInformation("  - MicrosoftAppTenantId: {TenantId}", !string.IsNullOrEmpty(tenantId) ? $"{tenantId[..8]}..." : "NOT_SET");
    
    return new ConfigurationBotFrameworkAuthentication(configuration);
});

// Create the Bot Framework Adapter with error handling enabled.
builder.Services.AddSingleton<IBotFrameworkHttpAdapter, AdapterWithErrorHandler>();

// Create the storage we'll be using for User and Conversation state.
builder.Services.AddSingleton<IStorage>(provider =>
{
    var connectionString = builder.Configuration.GetConnectionString("BlobStorage"); // This will now correctly pick up the mapped value if it was set
    if (!string.IsNullOrEmpty(connectionString))
    {
        try
        {
            Console.WriteLine("Initializing Bot state storage with Blob Storage");
            // Use an older API version for the blob storage used by the bot framework
            return new Microsoft.Bot.Builder.Azure.Blobs.BlobsStorage(connectionString, "bot-state");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing BlobsStorage for bot state: {ex.Message}. Falling back to memory storage.");
            return new MemoryStorage();
        }
    }
    
    // Fallback to memory storage for development
    Console.WriteLine("Blob Storage connection string not found. Using in-memory storage (not suitable for production).");
    return new MemoryStorage();
});

// Create the User state and Conversation state.
builder.Services.AddSingleton<UserState>();
builder.Services.AddSingleton<ConversationState>();

// Register the main bot
builder.Services.AddTransient<IBot, TeamsComplianceBot.Bots.TeamsComplianceBot>();
builder.Services.AddTransient<TeamsComplianceBot.Bots.TeamsComplianceBot>();

// Register compliance services - Changed from Singleton to Scoped to allow injection of scoped services
builder.Services.AddScoped<ICallPollingService, CallPollingService>();

// Configure HTTP client for Graph API with enhanced SSL/TLS handling
builder.Services.AddHttpClient("GraphClient", client =>
{
    client.Timeout = TimeSpan.FromMinutes(5);
    client.DefaultRequestHeaders.Add("User-Agent", "TeamsComplianceBot/1.0");
}).ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    
    // Configure SSL/TLS settings for Azure environments
    handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) =>
    {
        // For debugging purposes, log certificate details
        Console.WriteLine($"SSL Validation - Policy Errors: {sslPolicyErrors}");
        if (cert != null)
        {
            Console.WriteLine($"SSL Validation - Certificate Subject: {cert.Subject}");
            Console.WriteLine($"SSL Validation - Certificate Issuer: {cert.Issuer}");
        }
        
        // If there are no SSL policy errors, certificate is valid
        if (sslPolicyErrors == System.Net.Security.SslPolicyErrors.None)
        {
            Console.WriteLine("SSL certificate validation passed - No errors");
            return true;
        }
        
        // For Microsoft endpoints, be more permissive but still check for critical issues
        if (cert != null)
        {
            var subject = cert.Subject?.ToLowerInvariant() ?? "";
            var issuer = cert.Issuer?.ToLowerInvariant() ?? "";
            
            // Check if this is a Microsoft/Azure certificate
            var isMicrosoftCert = subject.Contains("microsoft.com") || 
                                subject.Contains("graph.microsoft.com") ||
                                subject.Contains("login.microsoftonline.com") ||
                                subject.Contains("azure.com") ||
                                issuer.Contains("microsoft") ||
                                issuer.Contains("digicert") ||
                                issuer.Contains("baltimore") ||
                                issuer.Contains("cybertrust");
                                
            if (isMicrosoftCert)
            {
                // For Microsoft certificates, only reject if there are critical errors
                var hasCriticalErrors = sslPolicyErrors.HasFlag(System.Net.Security.SslPolicyErrors.RemoteCertificateNotAvailable) ||
                                      (chain?.ChainStatus?.Any(status => status.Status == System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.Revoked) ?? false);
                
                if (!hasCriticalErrors)
                {
                    Console.WriteLine($"Allowing Microsoft certificate despite minor SSL policy errors: {sslPolicyErrors}");
                    return true;
                }
            }
        }
        
        Console.WriteLine($"SSL certificate validation failed with errors: {sslPolicyErrors}");
        return false;
    };
    
    // Enable all modern TLS versions
    handler.SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13;
    
    return handler;
});

// Configure Microsoft Graph with enhanced error handling
builder.Services.AddSingleton<GraphServiceClient>(provider =>
{
    var tenantId = builder.Configuration["AzureAd:TenantId"];
    var clientId = builder.Configuration["AzureAd:ClientId"];
    var clientSecret = builder.Configuration["AzureAd:ClientSecret"];
    var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();

    Console.WriteLine($"Configuring GraphServiceClient - TenantId: {tenantId?.Substring(0, 8)}..., ClientId: {clientId?.Substring(0, 8)}...");

    // Check if required values are missing
    if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
    {
        Console.WriteLine("WARNING: Azure AD credentials not fully configured. Graph API functionality will be limited.");
        
        // Try to use DefaultAzureCredential as fallback (works if app has managed identity)
        try
        {
            var defaultCredential = new DefaultAzureCredential();
            var httpClient = httpClientFactory.CreateClient("GraphClient");
            Console.WriteLine("Using DefaultAzureCredential with custom HttpClient");
            return new GraphServiceClient(httpClient, defaultCredential);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize GraphServiceClient with DefaultAzureCredential: {ex.Message}");
            throw new InvalidOperationException("Azure AD credentials not configured and DefaultAzureCredential failed.");
        }
    }

    var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
    var customHttpClient = httpClientFactory.CreateClient("GraphClient");
    Console.WriteLine("Using ClientSecretCredential with custom HttpClient");
    return new GraphServiceClient(customHttpClient, credential);
});

// Configure Azure Blob Storage for recordings with enhanced versioning and error handling
builder.Services.AddSingleton<BlobServiceClient>(provider =>
{
    var logger = provider.GetRequiredService<ILogger<Program>>();
    var connectionString = builder.Configuration.GetConnectionString("BlobStorage"); // This will also pick up mapped value
    
    if (!string.IsNullOrEmpty(connectionString))
    {
        try
        {
            logger.LogInformation("Initializing BlobServiceClient with connection string authentication");
            
            // Try multiple versions of the API in order of preference
            // Start with the oldest version to ensure maximum compatibility
            BlobClientOptions? clientOptions = null;
            BlobServiceClient? client = null;
            Exception? lastException = null;
            
            // Try API version fallback sequence
            var versions = new[]
            {
                BlobClientOptions.ServiceVersion.V2019_12_12, // Oldest, most compatible
                BlobClientOptions.ServiceVersion.V2020_02_10,
                BlobClientOptions.ServiceVersion.V2020_04_08,
                BlobClientOptions.ServiceVersion.V2020_06_12,
                BlobClientOptions.ServiceVersion.V2020_10_02,
                BlobClientOptions.ServiceVersion.V2021_02_12,
                BlobClientOptions.ServiceVersion.V2021_04_10,
                BlobClientOptions.ServiceVersion.V2021_06_08, // Azure Storage Emulator should support up to here
                BlobClientOptions.ServiceVersion.V2021_08_06,
                BlobClientOptions.ServiceVersion.V2021_10_04,
                BlobClientOptions.ServiceVersion.V2021_12_02,
                BlobClientOptions.ServiceVersion.V2022_11_02
            };
            
            foreach (var version in versions)
            {
                try
                {
                    clientOptions = new BlobClientOptions(version);
                    client = new BlobServiceClient(connectionString, clientOptions);
                    
                    // Test the connection with a simple operation
                    var containers = client.GetBlobContainers().Take(1).ToList();
                    
                    // If we get here, the connection works!
                    logger.LogInformation("BlobServiceClient initialized successfully with API version {ApiVersion}", version);
                    break;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    logger.LogWarning("Failed to initialize BlobServiceClient with API version {ApiVersion}: {Message}", 
                        version, ex.Message);
                    
                    // Try the next version
                    client = null;
                }
            }
            
            if (client == null)
            {
                // If all versions failed, try once more with the oldest version but without testing
                logger.LogWarning("All API versions failed. Attempting final fallback without testing connection...");
                clientOptions = new BlobClientOptions(BlobClientOptions.ServiceVersion.V2019_12_12);
                client = new BlobServiceClient(connectionString, clientOptions);
            }
            
            logger.LogInformation("BlobServiceClient initialization complete");
            return client;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize BlobServiceClient with connection string");
            throw new InvalidOperationException("Failed to initialize Azure Blob Storage client. Ensure the storage account exists and connection string is valid.", ex);
        }
    }

    logger.LogError("BlobStorage connection string is missing from configuration");
    throw new InvalidOperationException("BlobStorage connection string is required but not found in configuration");
});

// Register Graph subscription service
builder.Services.AddSingleton<IGraphSubscriptionService, GraphSubscriptionService>();

// Register the subscription renewal background service
builder.Services.AddHostedService<SubscriptionRenewalService>();

// Register custom services
builder.Services.AddScoped<ICallRecordingService, CallRecordingService>();
builder.Services.AddScoped<IComplianceService, ComplianceService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ICallJoiningService>(provider =>
{
    var graphClient = provider.GetRequiredService<GraphServiceClient>();
    var logger = provider.GetRequiredService<ILogger<CallJoiningService>>();
    var configuration = provider.GetRequiredService<IConfiguration>();
    return new CallJoiningService(graphClient, logger, configuration);
});

// Add health checks with improved blob storage checks
builder.Services.AddHealthChecks()
    .AddCheck("blob_storage", () =>
    {
        try
        {
            var connectionString = builder.Configuration.GetConnectionString("BlobStorage"); // Also picks up mapped value
            if (string.IsNullOrEmpty(connectionString))
            {
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("Blob storage connection string not configured");
            }
            
            // Try to create client with the most compatible API version
            var clientOptions = new BlobClientOptions(BlobClientOptions.ServiceVersion.V2019_12_12);
            var blobClient = new BlobServiceClient(connectionString, clientOptions);
            
            try
            {
                // Just check if we can access the service (don't enumerate containers as that might fail)
                var serviceProperties = blobClient.GetProperties();
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Blob storage is accessible");
            }
            catch (Exception ex)
            {
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded($"Blob storage service properties check failed: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("Blob storage is not accessible", ex);
        }
    });
builder.Services.AddAzureClients(clientBuilder =>
{
    clientBuilder.AddBlobServiceClient(builder.Configuration["StorageConnection:blobServiceUri"]!).WithName("StorageConnection");
    clientBuilder.AddQueueServiceClient(builder.Configuration["StorageConnection:queueServiceUri"]!).WithName("StorageConnection");
    clientBuilder.AddTableServiceClient(builder.Configuration["StorageConnection:tableServiceUri"]!).WithName("StorageConnection");
});

var app = builder.Build();

// Configure request localization to prevent culture issues
var supportedCultures = new[] { "en-US", "en" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture(supportedCultures[0])
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

// Enhanced culture handling to prevent CultureNotFoundException
localizationOptions.FallBackToParentCultures = true;
localizationOptions.FallBackToParentUICultures = true;
localizationOptions.RequestCultureProviders.Clear();
localizationOptions.RequestCultureProviders.Add(new Microsoft.AspNetCore.Localization.CookieRequestCultureProvider());
localizationOptions.RequestCultureProviders.Add(new SafeAcceptLanguageHeaderRequestCultureProvider());

app.UseRequestLocalization(localizationOptions);

// CRITICAL: Add culture safety middleware FIRST to prevent CultureNotFoundException crashes
app.UseMiddleware<CultureSafetyMiddleware>();

// Add request logging middleware for debugging BadRequest errors
app.UseMiddleware<RequestLoggingMiddleware>();

// Add global exception handling middleware
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// IMPORTANT: Don't use UseHttpsRedirection() in Azure App Service
// Azure handles HTTPS termination at the load balancer level
// app.UseHttpsRedirection(); // REMOVED - causes 400 errors in Azure

// Use CORS before routing and controllers
app.UseCors("AllowTeamsAndAzure");

// 🔧 Configure forwarded headers for Azure App Service
// Azure App Service terminates HTTPS at the load balancer and forwards HTTP to the app
// This fixes the port 80 HTTPS issue by properly handling Azure's proxy setup
var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto,
    RequireHeaderSymmetry = false
};
// Clear known networks and proxies to accept all forwarded headers from Azure
forwardedOptions.KnownNetworks.Clear();
forwardedOptions.KnownProxies.Clear();

// Configure the app to trust forwarded headers from Azure App Service
app.UseForwardedHeaders(forwardedOptions);

// Add Bot Framework authentication debugging middleware
app.UseMiddleware<BotAuthenticationDebugMiddleware>();

// Enable serving static files (including favicon.ico)
app.UseStaticFiles();

// NOTE: Removed custom scheme/host manipulation middleware that was causing 
// "HTTPS server variable not allowed" errors in Azure App Service.
// Azure App Service handles HTTPS termination and forwarding correctly via UseForwardedHeaders above.

app.UseRouting();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// Make Program class public for testing
public partial class Program { }
