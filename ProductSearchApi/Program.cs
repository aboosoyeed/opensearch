var builder = WebApplication.CreateBuilder(args);

Console.WriteLine("=== Product Search API ===");
Console.WriteLine("Core Product Search Engine - Functional Requirements Implementation");
Console.WriteLine();

// Add services to the container (Dependency Injection)
builder.Services.AddControllers();

// Register OpenSearch client
builder.Services.AddSingleton<OpenSearch.Client.IOpenSearchClient>(provider =>
{
    var configuration = provider.GetRequiredService<IConfiguration>();
    var opensearchUrl = Environment.GetEnvironmentVariable("OPENSEARCH_URL") 
        ?? configuration["OpenSearch:Url"] 
        ?? "http://localhost:9200";
    var settings = new OpenSearch.Client.ConnectionSettings(new Uri(opensearchUrl))
        .DefaultIndex("products")
        .EnableDebugMode()
        .PrettyJson()
        .ThrowExceptions(false);
    return new OpenSearch.Client.OpenSearchClient(settings);
});

// Register our product service - now using OpenSearch as data persistence layer
builder.Services.AddSingleton<ProductSearchApi.Services.IProductService, ProductSearchApi.Services.OpenSearchProductService>();

// Register OpenSearch service
builder.Services.AddSingleton<ProductSearchApi.Services.IOpenSearchService, ProductSearchApi.Services.OpenSearchService>();


// Configure JSON serialization options
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.SerializerOptions.WriteIndented = true;
});

// Add API documentation (Swagger/OpenAPI)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { 
        Title = "Product Search API", 
        Version = "v1",
        Description = "A simple API for product search and management"
    });
});

// Add CORS for cross-origin requests
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Build the application
var app = builder.Build();

// Initialize OpenSearch index with proper mappings
using (var scope = app.Services.CreateScope())
{
    var openSearchService = scope.ServiceProvider.GetRequiredService<ProductSearchApi.Services.IOpenSearchService>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        Console.WriteLine("Initializing OpenSearch index...");
        
        // Check connection first
        var pingResult = await openSearchService.PingAsync();
        if (!pingResult)
        {
            logger.LogWarning("Could not connect to OpenSearch. Index creation skipped.");
            Console.WriteLine("⚠️  Warning: Could not connect to OpenSearch. Index creation skipped.");
        }
        else
        {
            // Create index with proper mappings
            var indexCreated = await openSearchService.CreateIndexAsync();
            if (indexCreated)
            {
                Console.WriteLine("✓ OpenSearch index initialized successfully");
            }
            else
            {
                Console.WriteLine("✓ OpenSearch index already exists or creation handled by service");
            }
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error initializing OpenSearch index");
        Console.WriteLine($"⚠️  Error initializing OpenSearch index: {ex.Message}");
        // Continue running even if index creation fails - OpenSearch will auto-create with defaults
    }
}

// Configure the HTTP request pipeline (Middleware)
Console.WriteLine("Configuring middleware pipeline...");

// Development-only middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Product Search API v1");
        c.RoutePrefix = string.Empty; // Swagger UI at root
    });
    Console.WriteLine("✓ Swagger UI enabled at root URL");
}

// Security and routing middleware
app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();

// Map controllers to routes
app.MapControllers();

// Log startup information
Console.WriteLine();
Console.WriteLine("🚀 Product Search API is starting...");
Console.WriteLine($"📍 Environment: {app.Environment.EnvironmentName}");
Console.WriteLine($"🔗 Base URL: https://localhost:5000");
Console.WriteLine($"📖 Swagger UI: https://localhost:5000");
Console.WriteLine();
Console.WriteLine("Core Functional Requirements:");
Console.WriteLine();
Console.WriteLine("1. Product Ingestion API:");
Console.WriteLine("  POST   /api/Product           - Ingest single product (JSON format)");
Console.WriteLine("  POST   /api/Product/bulk      - Ingest multiple products (JSON catalog)");
Console.WriteLine("  GET    /api/Product/{id}      - Get product by ID");
Console.WriteLine("  GET    /api/Product           - Get all products");
Console.WriteLine();
Console.WriteLine("2. Search API (Core Requirement 2 - Full-text search with filtering, sorting, pagination):");
Console.WriteLine("  GET    /api/Search            - Search with query parameters");
Console.WriteLine("  POST   /api/Search            - Advanced search with JSON body");
Console.WriteLine();
Console.WriteLine("3. Guided Navigation (Faceted Search):");
Console.WriteLine("  GET    /api/FacetedSearch       - Get available filters and counts (query params)");
Console.WriteLine("  POST   /api/FacetedSearch       - Get available filters and counts (JSON body)");
Console.WriteLine();
Console.WriteLine("4. Search Suggestion API:");
Console.WriteLine("  GET    /api/SearchSuggestions/complete - Autocomplete suggestions based on input prefix");
Console.WriteLine();

app.Run();
