# Product Search Engine with OpenSearch and .NET

A proof-of-concept product search engine using OpenSearch as both search engine and data persistence layer, built with .NET Web API and featuring guided navigation (faceted search). Designed for future AI/Vector Search integration.

## Quick Start

### Prerequisites
- .NET 7.0 or later
- Docker and Docker Compose
- OpenSearch (via Docker)

### Running the Application

1. **Start OpenSearch**:
   ```bash
   docker-compose up -d
   ```

2. **Run the API**:
   ```bash
   cd ProductSearchApi
   dotnet run
   ```


The API will be available at `http://localhost:5000` with Swagger documentation at `http://localhost:5000/swagger`.

## 📋 Core Functional Requirements

This implementation fulfills all four core functional requirements from the specification:

### ✅ 1. Product Ingestion API
- **Single Product**: `POST /api/Product` - Creates products directly in OpenSearch
- **Bulk Ingestion**: `POST /api/Product/bulk` - Efficient bulk operations using OpenSearch bulk API
- **Get All Products**: `GET /api/Product` - Retrieves all products from OpenSearch
- **Get Product by ID**: `GET /api/Product/{id}` - Retrieves specific product
- **Format**: JSON with fields: title, description, category, price, brand, attributes
- **Persistence**: All products immediately available for search after creation

### ✅ 2. Search API
- **GET Search**: `GET /api/Search` - Direct OpenSearch queries with query parameters
- **POST Search**: `POST /api/Search` - Advanced search with JSON body for complex filtering
- **Features**: Full-text search over title and description, category/brand/price filtering, relevance/price sorting, pagination
- **Performance**: Native OpenSearch query performance with score-based relevance and field boosting (title^2)

### ✅ 3. Guided Navigation (Faceted Search)
- **GET Facets**: `GET /api/FacetedSearch` - Real-time facets using OpenSearch aggregations with query parameters
- **POST Facets**: `POST /api/FacetedSearch` - Faceted search with JSON body for complex filtering
- **Features**: Category, brand, and price range facets with accurate counts for current search result set
- **Performance**: Efficient aggregation queries for instant facet calculation

### ✅ 4. Search Suggestion API
- **Autocomplete**: `GET /api/SearchSuggestions/complete` - Fast prefix matching across product fields
- **Features**: Autocomplete suggestions from live OpenSearch data with product, brand, and category suggestions
- **Real-time**: Suggestions reflect current product inventory with boosted title matches

## Architecture & Design Decisions

### Technology Stack
- **Backend**: ASP.NET Core 7.0 Web API
- **Search Engine**: OpenSearch 2.x
- **Client Library**: OpenSearch.Client for .NET
- **Data Storage**: OpenSearch as primary persistence layer (for the purpose of this POC. Production would have a seperate persistance layer)
- **Documentation**: Swagger/OpenAPI

### Key Design Decisions

#### 1. **Clean Architecture Principles**
- **Separation of Concerns**: Controllers handle HTTP, Services handle business logic
- **Dependency Injection**: Interfaces for all services enable testability and flexibility
- **Single Responsibility**: Each controller focuses on one core requirement

#### 2. **OpenSearch Integration Strategy**
- **Single Source of Truth**: OpenSearch serves as both search engine and data persistence layer
- **Direct Client Usage**: OpenSearch.Client for maximum control over queries
- **Query DSL**: Leverages OpenSearch's powerful query capabilities
- **Index Management**: Dedicated service for index operations and mappings
- **Real-time Operations**: Immediate refresh ensures data consistency across API operations

#### 3. **API Design Philosophy**
- **RESTful Principles**: Resource-based URLs with appropriate HTTP methods
- **Consistent Response Format**: Standardized JSON responses with metadata
- **Error Handling**: Comprehensive error responses with details
- **Validation**: Request validation with meaningful error messages

#### 4. **Search Strategy**
```
Traditional Keyword Search ──→ Advanced Filtering ──→ Faceted Navigation
                                      
Future: AI/Vector Search ──→ Semantic Understanding
```


## API Documentation

Based on the core functional requirements from the specification, the API provides four main endpoint groups:

### 1. Product Ingestion API (Core Requirement 1)

#### Create Single Product
```http
POST /api/Product
Content-Type: application/json

{
  "title": "Gaming Laptop",
  "description": "High-performance gaming laptop with RTX graphics",
  "category": "Electronics",
  "price": 1299.99,
  "brand": "TechBrand",
  "attributes": {
    "RAM": "16GB",
    "Storage": "1TB SSD"
  }
}
```

#### Bulk Product Creation
```http
POST /api/Product/bulk
Content-Type: application/json

{
  "products": [
    {
      "title": "Wireless Mouse",
      "description": "Ergonomic wireless mouse",
      "category": "Electronics",
      "price": 79.99,
      "brand": "TechBrand"
    }
  ]
}
```

#### Get All Products
```http
GET /api/Product
```

#### Get Product By ID
```http
GET /api/Product/123
```

### 2. Search API (Core Requirement 2)

Full-text search over title and description with filtering by category, brand, and price range. Sorting by relevance or price. Pagination support.

#### GET Search (Simple)
```http
GET /api/Search?query=gaming laptop&category=Electronics&brand=TechBrand&minPrice=500&maxPrice=2000&sort=price_asc&page=1&size=10
```

#### POST Search (Advanced)
```http
POST /api/Search
Content-Type: application/json

{
  "query": "laptop",
  "filters": {
    "category": "Electronics",
    "brand": "TechBrand",
    "minPrice": 500,
    "maxPrice": 2000
  },
  "sort": "price_asc",
  "page": 1,
  "size": 20
}
```

**Response:**
```json
{
  "query": "laptop",
  "filters": {
    "category": "Electronics",
    "brand": "TechBrand",
    "minPrice": 500,
    "maxPrice": 2000
  },
  "sort": "price_asc",
  "pagination": { "page": 1, "size": 20 },
  "results": {
    "total": 15,
    "page": 1,
    "pageSize": 20,
    "totalPages": 1,
    "products": [
      {
        "id": 1,
        "title": "Gaming Laptop",
        "description": "High-performance gaming laptop",
        "category": "Electronics",
        "brand": "TechBrand",
        "price": 1299.99,
        "score": 2.1
      }
    ]
  },
  "searchType": "Advanced Search with Filtering, Sorting, and Pagination",
  "description": "Core Requirement 2: Full-text search with category/brand/price filtering, sorting by relevance/price, pagination support"
}
```

### 3. Guided Navigation (Faceted Search) - Core Requirement 3

Return available filters and counts for current search result set (e.g. "Brand: Nike (12), Adidas (5)").

#### GET Faceted Search
```http
GET /api/FacetedSearch?query=laptop&category=Electronics&size=10
```

#### POST Faceted Search
```http
POST /api/FacetedSearch
Content-Type: application/json

{
  "query": "laptop",
  "category": "Electronics",
  "brand": "TechBrand",
  "minPrice": 500,
  "maxPrice": 2000,
  "size": 10
}
```

**Response:**
```json
{
  "searchQuery": "laptop",
  "totalResults": 25,
  "timeTaken": 15,
  "results": [
    {
      "id": 1,
      "title": "Gaming Laptop",
      "category": "Electronics",
      "brand": "TechBrand",
      "price": 1299.99
    }
  ],
  "facets": {
    "categories": [
      { "category": "Electronics", "count": 20 },
      { "category": "Gaming", "count": 5 }
    ],
    "brands": [
      { "brand": "TechBrand", "count": 15 },
      { "brand": "GameCorp", "count": 10 }
    ],
    "priceRanges": [
      { "range": "*-50.0", "count": 5, "from": null, "to": 50.0 },
      { "range": "50.0-100.0", "count": 10, "from": 50.0, "to": 100.0 },
      { "range": "100.0-250.0", "count": 8, "from": 100.0, "to": 250.0 },
      { "range": "250.0-500.0", "count": 12, "from": 250.0, "to": 500.0 },
      { "range": "500.0-1000.0", "count": 7, "from": 500.0, "to": 1000.0 },
      { "range": "1000.0-*", "count": 3, "from": 1000.0, "to": null }
    ]
  },
  "facetType": "Guided Navigation Facets",
  "description": "Core Requirement 3: Available filters and counts for current search result set"
}
```

### 4. Search Suggestion API (Core Requirement 4)

Return autocomplete suggestions based on search input prefix (OpenSearch suggesters).

#### Get Autocomplete Suggestions
```http
GET /api/SearchSuggestions/complete?query=gam&size=5
```

**Response:**
```json
{
  "query": "gam",
  "suggestions": [
    {
      "text": "Gaming Laptop",
      "type": "Product",
      "category": "Electronics",
      "brand": "TechBrand"
    },
    {
      "text": "Gaming Mouse",
      "type": "Product", 
      "category": "Electronics",
      "brand": "Razer"
    },
    {
      "text": "GameCorp",
      "type": "Brand",
      "productCount": 5
    }
  ],
  "totalSuggestions": 3,
  "suggesterType": "Autocomplete Suggestions",
  "description": "Core Requirement 4: Autocomplete suggestions based on search input prefix"
}
```

## Future-Readiness Plan for AI/Vector Search

This POC is designed with minimalistic architecture to support future AI and vector search capabilities. The foundation interfaces (`IVectorSearchService` and `ISearchOrchestrator`) are already implemented to enable seamless integration. 

### Phase 1: Vector Search Foundation (Ready)
- **Interfaces**: Core search orchestration and vector search service interfaces are in place
- **Architecture**: Modular design allows plugging in vector-based search without disrupting existing functionality
- **Configuration**: Feature flags ready for gradual rollout of AI features

### Phase 2: Semantic Search Integration
- **OpenSearch Vector Engine**: Leverage native OpenSearch vector capabilities with dense vector fields
- **Embedding Generation**: Integrate modern transformer models (Sentence Transformers, OpenAI embeddings)
- **Hybrid Search**: Combine traditional keyword search with semantic similarity matching

### Phase 3: Natural Language Processing
- **Query Understanding**: Extract filters and intent from natural language queries ("show me black sneakers under $100")
- **Semantic Matching**: Enable searches based on meaning rather than exact keyword matches
- **Context Awareness**: Consider user behavior and search history for personalized results

### Phase 4: Advanced AI Features
- **Multi-Modal Search**: Support image-based product search alongside text


### API Testing
This POC focuses on functional API testing rather than unit tests. For comprehensive end-to-end testing examples, curl commands, and sample data, see **[API_TESTING.md](API_TESTING.md)**.

The testing guide includes:
- Health check endpoints
- Product ingestion (single and bulk)
- Search functionality with various filters
- Faceted search testing
- Autocomplete suggestions
- OpenSearch direct queries
- Sample test data sets

### OpenSearch Management

#### Index Management
The products index is automatically created with proper mappings when the application starts. During startup, the application:
1. Connects to OpenSearch
2. Checks if the `products` index exists
3. Creates it with optimized mappings if it doesn't exist
4. Logs the initialization status

This ensures all search features work correctly with the proper field types (keyword for facets, text for search, etc.).






