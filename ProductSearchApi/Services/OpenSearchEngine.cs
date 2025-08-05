using OpenSearch.Client;
using OpenSearch.Net;
using ProductSearchApi.Interfaces;
using ProductSearchApi.DTOs;

namespace ProductSearchApi.Services
{
    /// <summary>
    /// OpenSearch implementation of ISearchEngine
    /// </summary>
    public class OpenSearchEngine : ISearchEngine
    {
        private readonly IOpenSearchClient _client;
        private readonly ILogger<OpenSearchEngine> _logger;
        private const string IndexName = "products";

        public OpenSearchEngine(ILogger<OpenSearchEngine> logger, IConfiguration configuration)
        {
            _logger = logger;

            // Get OpenSearch URL from environment variable, configuration, or use default
            var opensearchUrl = Environment.GetEnvironmentVariable("OPENSEARCH_URL") 
                ?? configuration["OpenSearch:Url"] 
                ?? "http://localhost:9200";
            
            // Create connection settings
            var settings = new ConnectionSettings(new Uri(opensearchUrl))
                .DefaultIndex(IndexName)
                .EnableDebugMode()
                .PrettyJson()
                .ThrowExceptions(false)
                .ServerCertificateValidationCallback((sender, certificate, chain, errors) => true);

            _client = new OpenSearchClient(settings);
            
            _logger.LogInformation("OpenSearch engine initialized with URL: {Url}", opensearchUrl);
        }

        public async Task<IndexResult> IndexDocumentAsync<T>(T document, int? id = null) where T : class
        {
            try
            {
                var response = id.HasValue 
                    ? await _client.IndexAsync(document, i => i.Index(IndexName).Id(id.Value).Refresh(Refresh.True))
                    : await _client.IndexAsync(document, i => i.Index(IndexName).Refresh(Refresh.True));

                return new IndexResult
                {
                    Success = response.IsValid,
                    Id = response.Id,
                    Error = response.IsValid ? null : response.DebugInformation
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error indexing document");
                return new IndexResult { Success = false, Error = ex.Message };
            }
        }

        public async Task<BulkIndexResult> BulkIndexDocumentsAsync<T>(IEnumerable<T> documents) where T : class
        {
            try
            {
                var bulkDescriptor = new BulkDescriptor();
                foreach (var doc in documents)
                {
                    bulkDescriptor.Index<T>(op => op
                        .Index(IndexName)
                        .Document(doc)
                    );
                }

                var response = await _client.BulkAsync(bulkDescriptor);
                
                var errors = new List<string>();
                if (response.ItemsWithErrors.Any())
                {
                    foreach (var item in response.ItemsWithErrors)
                    {
                        if (item.Error != null)
                        {
                            errors.Add($"Error: {item.Error.Type} - {item.Error.Reason}");
                        }
                    }
                }

                return new BulkIndexResult
                {
                    Success = response.IsValid && !response.Errors,
                    ItemsIndexed = response.Items.Count - response.ItemsWithErrors.Count(),
                    Errors = errors
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk indexing documents");
                return new BulkIndexResult { Success = false, Errors = { ex.Message } };
            }
        }

        public async Task<T?> GetDocumentAsync<T>(int id) where T : class
        {
            try
            {
                var response = await _client.GetAsync<T>(id, g => g.Index(IndexName));
                return response.Found ? response.Source : default;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting document {Id}", id);
                return default;
            }
        }

        public async Task<bool> DeleteDocumentAsync(int id)
        {
            try
            {
                var response = await _client.DeleteAsync<Product>(id, d => d.Index(IndexName).Refresh(Refresh.True));
                return response.IsValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting document {Id}", id);
                return false;
            }
        }

        public async Task<SearchResult<T>> SearchAsync<T>(SearchQuery query) where T : class
        {
            try
            {
                var searchRequest = BuildSearchRequest<T>(query);
                var response = await _client.SearchAsync<T>(searchRequest);

                var hits = new List<SearchHit<T>>();
                if (response.HitsMetadata?.Hits != null)
                {
                    foreach (var hit in response.HitsMetadata.Hits)
                    {
                        hits.Add(new SearchHit<T>
                        {
                            Source = hit.Source,
                            Score = hit.Score
                        });
                    }
                }

                return new SearchResult<T>
                {
                    Documents = response.Documents,
                    Total = response.Total,
                    Page = query.Page,
                    Size = query.Size,
                    Hits = hits
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching documents");
                return new SearchResult<T> { Documents = new List<T>(), Total = 0 };
            }
        }

        public async Task<IEnumerable<T>> GetAllAsync<T>() where T : class
        {
            try
            {
                var response = await _client.SearchAsync<T>(s => s
                    .Index(IndexName)
                    .Query(q => q
                        .Bool(b => b
                            .Must(m => m.Term(t => t.Field("isActive").Value(true)))
                        )
                    )
                    .Size(1000)
                );

                return response.IsValid ? response.Documents : new List<T>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all documents");
                return new List<T>();
            }
        }

        public async Task<IEnumerable<SearchSuggestion>> GetSuggestionsAsync(string prefix, int size = 5)
        {
            try
            {
                var response = await _client.SearchAsync<Product>(s => s
                    .Index(IndexName)
                    .Size(size * 3)
                    .Query(q => q
                        .Bool(b => b
                            .Should(
                                sh => sh.Prefix(p => p.Field(f => f.Title).Value(prefix).Boost(3)),
                                sh => sh.Prefix(p => p.Field(f => f.Brand).Value(prefix).Boost(2)),
                                sh => sh.Prefix(p => p.Field(f => f.Category).Value(prefix).Boost(1))
                            )
                        )
                    )
                );

                var suggestions = new List<SearchSuggestion>();
                var uniqueSuggestions = new HashSet<string>();

                foreach (var product in response.Documents)
                {
                    // Extract suggestions from title
                    if (!string.IsNullOrWhiteSpace(product.Title) && 
                        product.Title.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                        uniqueSuggestions.Add(product.Title.ToLower()))
                    {
                        suggestions.Add(new SearchSuggestion
                        {
                            Text = product.Title,
                            Type = "Product",
                            Metadata = new Dictionary<string, object>
                            {
                                { "category", product.Category ?? "" },
                                { "brand", product.Brand ?? "" }
                            }
                        });
                    }

                    // Extract suggestions from brand
                    if (!string.IsNullOrWhiteSpace(product.Brand) && 
                        product.Brand.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                        uniqueSuggestions.Add(product.Brand.ToLower()))
                    {
                        suggestions.Add(new SearchSuggestion
                        {
                            Text = product.Brand,
                            Type = "Brand",
                            Metadata = new Dictionary<string, object>
                            {
                                { "productCount", response.Documents.Count(p => p.Brand == product.Brand) }
                            }
                        });
                    }

                    if (suggestions.Count >= size) break;
                }

                return suggestions.Take(size);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting suggestions");
                return new List<SearchSuggestion>();
            }
        }

        public async Task<FacetedSearchResult<T>> SearchWithFacetsAsync<T>(FacetedSearchQuery query) where T : class
        {
            try
            {
                var searchRequest = BuildFacetedSearchRequest<T>(query);
                var response = await _client.SearchAsync<T>(searchRequest);

                var facets = new Dictionary<string, List<FacetBucket>>();

                // Process category facets
                if (response.Aggregations.ContainsKey("categories"))
                {
                    var categoriesAgg = response.Aggregations.Terms("categories");
                    facets["categories"] = categoriesAgg.Buckets.Select(b => new FacetBucket
                    {
                        Key = b.Key,
                        Count = (long)(b.DocCount ?? 0)
                    }).ToList();
                }

                // Process brand facets
                if (response.Aggregations.ContainsKey("brands"))
                {
                    var brandsAgg = response.Aggregations.Terms("brands");
                    facets["brands"] = brandsAgg.Buckets.Select(b => new FacetBucket
                    {
                        Key = b.Key,
                        Count = (long)(b.DocCount ?? 0)
                    }).ToList();
                }

                // Process price range facets
                if (response.Aggregations.ContainsKey("price_ranges"))
                {
                    var priceRangesAgg = response.Aggregations.Range("price_ranges");
                    facets["priceRanges"] = priceRangesAgg.Buckets.Select(b => new FacetBucket
                    {
                        Key = b.Key,
                        Count = b.DocCount,
                        From = b.From,
                        To = b.To
                    }).ToList();
                }

                return new FacetedSearchResult<T>
                {
                    Documents = response.Documents,
                    Total = response.Total,
                    Page = query.Page,
                    Size = query.Size,
                    Facets = facets
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in faceted search");
                return new FacetedSearchResult<T> { Documents = new List<T>(), Total = 0 };
            }
        }

        public async Task<bool> IsHealthyAsync()
        {
            try
            {
                var response = await _client.PingAsync();
                return response.IsValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking health");
                return false;
            }
        }

        #region Private Helper Methods

        private Func<SearchDescriptor<T>, ISearchRequest> BuildSearchRequest<T>(SearchQuery query) where T : class
        {
            return s =>
            {
                var searchDescriptor = s.Index(IndexName);

                var mustClauses = new List<QueryContainer>();
                var filterClauses = new List<QueryContainer>();

                // Always filter for active products
                filterClauses.Add(new TermQuery { Field = "isActive", Value = true });

                // Add text search if provided
                if (!string.IsNullOrWhiteSpace(query.Query))
                {
                    mustClauses.Add(new MultiMatchQuery
                    {
                        Query = query.Query,
                        Fields = new[] { "title^2", "description" },
                        Type = TextQueryType.BestFields,
                        Fuzziness = Fuzziness.Auto
                    });
                }

                // Add filters
                foreach (var filter in query.Filters)
                {
                    switch (filter.Key.ToLower())
                    {
                        case "category":
                            filterClauses.Add(new TermQuery { Field = "category", Value = filter.Value.ToString() });
                            break;
                        case "brand":
                            filterClauses.Add(new TermQuery { Field = "brand", Value = filter.Value.ToString() });
                            break;
                        case "minprice":
                            if (decimal.TryParse(filter.Value.ToString(), out var minPrice))
                            {
                                filterClauses.Add(new NumericRangeQuery 
                                { 
                                    Field = "price", 
                                    GreaterThanOrEqualTo = (double)minPrice 
                                });
                            }
                            break;
                        case "maxprice":
                            if (decimal.TryParse(filter.Value.ToString(), out var maxPrice))
                            {
                                filterClauses.Add(new NumericRangeQuery 
                                { 
                                    Field = "price", 
                                    LessThanOrEqualTo = (double)maxPrice 
                                });
                            }
                            break;
                    }
                }

                // Build final query
                if (mustClauses.Any() || filterClauses.Any())
                {
                    searchDescriptor = searchDescriptor.Query(q => new BoolQuery
                    {
                        Must = mustClauses.Any() ? mustClauses : new QueryContainer[] { new MatchAllQuery() },
                        Filter = filterClauses
                    });
                }
                else
                {
                    searchDescriptor = searchDescriptor.Query(q => q.MatchAll());
                }

                // Add sorting
                if (!string.IsNullOrWhiteSpace(query.SortBy))
                {
                    switch (query.SortBy.ToLower())
                    {
                        case "price":
                            searchDescriptor = query.SortDescending 
                                ? searchDescriptor.Sort(sort => sort.Descending("price"))
                                : searchDescriptor.Sort(sort => sort.Ascending("price"));
                            break;
                        case "relevance":
                        default:
                            searchDescriptor = searchDescriptor.Sort(sort => sort.Descending(SortSpecialField.Score));
                            break;
                    }
                }

                // Add pagination
                var from = (query.Page - 1) * query.Size;
                searchDescriptor = searchDescriptor.From(from).Size(query.Size);

                return searchDescriptor;
            };
        }

        private Func<SearchDescriptor<T>, ISearchRequest> BuildFacetedSearchRequest<T>(FacetedSearchQuery query) where T : class
        {
            return s =>
            {
                // Build the search with filters from the base method
                var searchDescriptor = s.Index(IndexName);

                var mustClauses = new List<QueryContainer>();
                var filterClauses = new List<QueryContainer>();

                // Always filter for active products
                filterClauses.Add(new TermQuery { Field = "isActive", Value = true });

                // Add text search if provided
                if (!string.IsNullOrWhiteSpace(query.Query))
                {
                    mustClauses.Add(new MultiMatchQuery
                    {
                        Query = query.Query,
                        Fields = new[] { "title^2", "description" },
                        Type = TextQueryType.BestFields,
                        Fuzziness = Fuzziness.Auto
                    });
                }

                // Add filters
                foreach (var filter in query.Filters)
                {
                    switch (filter.Key.ToLower())
                    {
                        case "category":
                            filterClauses.Add(new TermQuery { Field = "category", Value = filter.Value.ToString() });
                            break;
                        case "brand":
                            filterClauses.Add(new TermQuery { Field = "brand", Value = filter.Value.ToString() });
                            break;
                        case "minprice":
                            if (decimal.TryParse(filter.Value.ToString(), out var minPrice))
                            {
                                filterClauses.Add(new NumericRangeQuery 
                                { 
                                    Field = "price", 
                                    GreaterThanOrEqualTo = (double)minPrice 
                                });
                            }
                            break;
                        case "maxprice":
                            if (decimal.TryParse(filter.Value.ToString(), out var maxPrice))
                            {
                                filterClauses.Add(new NumericRangeQuery 
                                { 
                                    Field = "price", 
                                    LessThanOrEqualTo = (double)maxPrice 
                                });
                            }
                            break;
                    }
                }

                // Build final query
                if (mustClauses.Any() || filterClauses.Any())
                {
                    searchDescriptor = searchDescriptor.Query(q => new BoolQuery
                    {
                        Must = mustClauses.Any() ? mustClauses : new QueryContainer[] { new MatchAllQuery() },
                        Filter = filterClauses
                    });
                }
                else
                {
                    searchDescriptor = searchDescriptor.Query(q => q.MatchAll());
                }

                // Add sorting
                if (!string.IsNullOrWhiteSpace(query.SortBy))
                {
                    switch (query.SortBy.ToLower())
                    {
                        case "price":
                            searchDescriptor = query.SortDescending 
                                ? searchDescriptor.Sort(sort => sort.Descending("price"))
                                : searchDescriptor.Sort(sort => sort.Ascending("price"));
                            break;
                        case "relevance":
                        default:
                            searchDescriptor = searchDescriptor.Sort(sort => sort.Descending(SortSpecialField.Score));
                            break;
                    }
                }

                // Add pagination
                var from = (query.Page - 1) * query.Size;
                searchDescriptor = searchDescriptor.From(from).Size(query.Size);

                // Add aggregations
                searchDescriptor = searchDescriptor.Aggregations(aggs => aggs
                    // Category facet
                    .Terms("categories", t => t
                        .Field("category")
                        .Size(query.FacetSizes.GetValueOrDefault("categories", 20))
                        .Order(o => o.CountDescending())
                    )
                    // Brand facet
                    .Terms("brands", t => t
                        .Field("brand")
                        .Size(query.FacetSizes.GetValueOrDefault("brands", 15))
                        .Order(o => o.CountDescending())
                    )
                    // Price range facet
                    .Range("price_ranges", r => r
                        .Field("price")
                        .Ranges(
                            range => range.To(50),
                            range => range.From(50).To(100),
                            range => range.From(100).To(250),
                            range => range.From(250).To(500),
                            range => range.From(500).To(1000),
                            range => range.From(1000)
                        )
                    )
                );

                return searchDescriptor;
            };
        }

        #endregion
    }
}