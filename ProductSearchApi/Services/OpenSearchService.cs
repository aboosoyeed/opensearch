using OpenSearch.Client;
using OpenSearch.Net;

namespace ProductSearchApi.Services
{
    public class OpenSearchService : IOpenSearchService
    {
        private readonly IOpenSearchClient _client;
        private readonly ILogger<OpenSearchService> _logger;
        private const string IndexName = "products";

        public OpenSearchService(ILogger<OpenSearchService> logger, IConfiguration configuration)
        {
            _logger = logger;

            // Get OpenSearch URL from environment variable, configuration, or use default
            var opensearchUrl = Environment.GetEnvironmentVariable("OPENSEARCH_URL") 
                ?? configuration["OpenSearch:Url"] 
                ?? "http://localhost:9200";
            
            // Create connection settings
            var settings = new ConnectionSettings(new Uri(opensearchUrl))
                .DefaultIndex(IndexName)
                .EnableDebugMode() // Enable debug mode for development
                .PrettyJson() // Pretty print JSON for debugging
                .ThrowExceptions(false) // Don't throw exceptions, handle errors gracefully
                .ServerCertificateValidationCallback((sender, certificate, chain, errors) => true); // Accept any certificate in dev

            _client = new OpenSearchClient(settings);
            
            _logger.LogInformation("OpenSearch client initialized with URL: {Url}", opensearchUrl);
        }

        /// <summary>
        /// Test connection to OpenSearch
        /// </summary>
        public async Task<bool> PingAsync()
        {
            try
            {
                var pingResponse = await _client.PingAsync();
                
                if (pingResponse.IsValid)
                {
                    _logger.LogInformation("Successfully connected to OpenSearch");
                    return true;
                }
                
                _logger.LogError("Failed to ping OpenSearch: {Error}", pingResponse.DebugInformation);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pinging OpenSearch");
                return false;
            }
        }

        /// <summary>
        /// Create the products index with proper mappings
        /// </summary>
        public async Task<bool> CreateIndexAsync()
        {
            try
            {
                // Check if index already exists
                var existsResponse = await _client.Indices.ExistsAsync(IndexName);
                if (existsResponse.Exists)
                {
                    _logger.LogInformation("Index {IndexName} already exists", IndexName);
                    return true;
                }

                // Create index with mappings
                var createIndexResponse = await _client.Indices.CreateAsync(IndexName, c => c
                    .Settings(s => s
                        .NumberOfShards(1)
                        .NumberOfReplicas(0)
                        .RefreshInterval("1s") // Refresh every second for near real-time search
                    )
                    .Map<Product>(m => m
                        .Properties(p => p
                            .Number(n => n
                                .Name(product => product.Id)
                                .Type(NumberType.Integer)
                            )
                            .Text(t => t
                                .Name(product => product.Title)
                                .Analyzer("standard")
                                .Fields(f => f
                                    .Keyword(k => k
                                        .Name("keyword")
                                        .IgnoreAbove(256)
                                    )
                                )
                            )
                            .Text(t => t
                                .Name(product => product.Description)
                                .Analyzer("standard")
                            )
                            .Keyword(k => k
                                .Name(product => product.Category)
                            )
                            .Number(n => n
                                .Name(product => product.Price)
                                .Type(NumberType.Float)
                            )
                            .Keyword(k => k
                                .Name(product => product.Brand)
                            )
                            .Object<Dictionary<string, string>>(o => o
                                .Name(product => product.Attributes)
                                .Enabled()
                            )
                            .Date(d => d
                                .Name(product => product.CreatedDate)
                            )
                            .Boolean(b => b
                                .Name(product => product.IsActive)
                            )
                        )
                    )
                );

                if (createIndexResponse.IsValid)
                {
                    _logger.LogInformation("Successfully created index {IndexName}", IndexName);
                    return true;
                }

                _logger.LogError("Failed to create index: {Error}", createIndexResponse.DebugInformation);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating index");
                return false;
            }
        }
    }
}