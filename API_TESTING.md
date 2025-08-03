# API Testing Guide

This document contains comprehensive API testing examples for the Product Search Engine.

## Prerequisites
- Ensure the API is running at `http://localhost:5000`
- Ensure OpenSearch is running at `http://localhost:9200`

## Core API Testing

### 1. Health Check
```bash
curl http://localhost:5000/api/Product/health
```

### 2. Product Ingestion Testing

#### Create Single Product
```bash
curl -X POST http://localhost:5000/api/Product \
  -H "Content-Type: application/json" \
  -d '{"title":"Test Product","description":"Test","category":"Electronics","price":99.99,"brand":"TestBrand"}'
```

#### Create Gaming Laptop
```bash
curl -X POST http://localhost:5000/api/Product \
  -H "Content-Type: application/json" \
  -d '{"title":"Gaming Laptop","description":"High-performance gaming laptop with RTX graphics","category":"Electronics","price":1299.99,"brand":"TechBrand","attributes":{"RAM":"16GB","Storage":"1TB SSD"}}'
```

#### Create Wireless Mouse
```bash
curl -X POST http://localhost:5000/api/Product \
  -H "Content-Type: application/json" \
  -d '{"title":"Wireless Mouse","description":"Ergonomic wireless mouse","category":"Electronics","price":79.99,"brand":"TechBrand"}'
```

#### Bulk Product Creation
```bash
curl -X POST http://localhost:5000/api/Product/bulk \
  -H "Content-Type: application/json" \
  -d '{
    "products": [
      {
        "title": "Gaming Keyboard",
        "description": "Mechanical gaming keyboard with RGB lighting",
        "category": "Electronics",
        "price": 149.99,
        "brand": "GameCorp"
      },
      {
        "title": "4K Monitor",
        "description": "Ultra-wide 4K gaming monitor",
        "category": "Electronics",
        "price": 599.99,
        "brand": "DisplayTech"
      },
      {
        "title": "Wireless Headphones",
        "description": "Noise-cancelling wireless headphones",
        "category": "Electronics",
        "price": 299.99,
        "brand": "AudioBrand"
      }
    ]
  }'
```

### 3. Product Retrieval Testing

#### Get All Products
```bash
curl "http://localhost:5000/api/Product"
```

#### Get Product by ID
```bash
curl "http://localhost:5000/api/Product/1"
```

### 4. Search API Testing

#### Basic Search
```bash
curl "http://localhost:5000/api/Search?query=gaming&size=5"
```

#### Search with Category Filter
```bash
curl "http://localhost:5000/api/Search?query=laptop&category=Electronics&size=5"
```

#### Search with Price Range
```bash
curl "http://localhost:5000/api/Search?query=gaming&minPrice=100&maxPrice=500&size=5"
```

#### Advanced Search with Multiple Filters
```bash
curl "http://localhost:5000/api/Search?query=gaming laptop&category=Electronics&brand=TechBrand&minPrice=500&maxPrice=2000&sort=price_asc&page=1&size=10"
```

#### POST Search (Advanced)
```bash
curl -X POST "http://localhost:5000/api/Search" \
  -H "Content-Type: application/json" \
  -d '{
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
  }'
```

### 5. Faceted Search Testing

#### Basic Faceted Search
```bash
curl "http://localhost:5000/api/FacetedSearch?query=gaming"
```

#### Faceted Search with Filters
```bash
curl "http://localhost:5000/api/FacetedSearch?query=laptop&category=Electronics&size=10"
```

#### POST Faceted Search
```bash
curl -X POST "http://localhost:5000/api/FacetedSearch" \
  -H "Content-Type: application/json" \
  -d '{
    "query": "gaming",
    "category": "Electronics",
    "brand": "TechBrand",
    "minPrice": 100,
    "maxPrice": 1000,
    "size": 10
  }'
```

### 6. Search Suggestions Testing

#### Basic Autocomplete
```bash
curl "http://localhost:5000/api/SearchSuggestions/complete?query=gam&size=5"
```

#### Autocomplete for "wireless"
```bash
curl "http://localhost:5000/api/SearchSuggestions/complete?query=wire&size=5"
```

#### Autocomplete for "tech"
```bash
curl "http://localhost:5000/api/SearchSuggestions/complete?query=tech&size=3"
```

## OpenSearch Direct Testing

### View All Products in OpenSearch
```bash
curl "http://localhost:9200/products/_search?pretty"
```

### Check OpenSearch Cluster Health
```bash
curl "http://localhost:9200/_cluster/health?pretty"
```

### View OpenSearch Indices
```bash
curl "http://localhost:9200/_cat/indices"
```

### View Index Mappings
```bash
curl "http://localhost:9200/products/_mapping?pretty"
```

### Search OpenSearch Directly
```bash
curl -X POST "http://localhost:9200/products/_search?pretty" \
  -H "Content-Type: application/json" \
  -d '{
    "query": {
      "match": {
        "title": "gaming"
      }
    }
  }'
```

## Development Operations

### Delete and Recreate Index (Development Only)
```bash
# Delete index
curl -X DELETE "http://localhost:9200/products"

# Restart API to recreate index with fresh mappings
# (The API will automatically recreate the index on startup)
```

### Count Total Products
```bash
curl "http://localhost:9200/products/_count?pretty"
```

## Test Data Sets

### Sample Product Data for Bulk Import
```json
{
  "products": [
    {
      "title": "iPhone 14 Pro",
      "description": "Latest iPhone with advanced camera system",
      "category": "Electronics",
      "price": 999.99,
      "brand": "Apple",
      "attributes": {
        "Storage": "128GB",
        "Color": "Space Black"
      }
    },
    {
      "title": "Samsung Galaxy S23",
      "description": "Flagship Android smartphone",
      "category": "Electronics",
      "price": 799.99,
      "brand": "Samsung",
      "attributes": {
        "Storage": "256GB",
        "Color": "Phantom Black"
      }
    },
    {
      "title": "MacBook Pro M2",
      "description": "Professional laptop with M2 chip",
      "category": "Electronics",
      "price": 1999.99,
      "brand": "Apple",
      "attributes": {
        "RAM": "16GB",
        "Storage": "512GB SSD"
      }
    },
    {
      "title": "Dell XPS 13",
      "description": "Ultra-portable business laptop",
      "category": "Electronics",
      "price": 1299.99,
      "brand": "Dell",
      "attributes": {
        "RAM": "8GB",
        "Storage": "256GB SSD"
      }
    },
    {
      "title": "Sony WH-1000XM4",
      "description": "Premium noise-cancelling headphones",
      "category": "Electronics",
      "price": 349.99,
      "brand": "Sony",
      "attributes": {
        "Type": "Over-ear",
        "Wireless": "Yes"
      }
    }
  ]
}
```

## Expected Response Patterns

### Successful Product Creation
```json
{
  "id": 1,
  "title": "Gaming Laptop",
  "description": "High-performance gaming laptop with RTX graphics",
  "category": "Electronics",
  "price": 1299.99,
  "brand": "TechBrand",
  "attributes": {
    "RAM": "16GB",
    "Storage": "1TB SSD"
  },
  "isActive": true,
  "createdAt": "2024-01-15T10:30:00Z"
}
```

### Search Results Pattern
```json
{
  "query": "gaming",
  "totalResults": 3,
  "page": 1,
  "pageSize": 5,
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
}
```

### Faceted Search Results Pattern
```json
{
  "searchQuery": "gaming",
  "totalResults": 3,
  "results": [...],
  "facets": {
    "categories": [
      { "category": "Electronics", "count": 3 }
    ],
    "brands": [
      { "brand": "TechBrand", "count": 2 },
      { "brand": "GameCorp", "count": 1 }
    ],
    "priceRanges": [
      { "range": "100.0-250.0", "count": 1, "from": 100.0, "to": 250.0 },
      { "range": "250.0-500.0", "count": 1, "from": 250.0, "to": 500.0 },
      { "range": "1000.0-*", "count": 1, "from": 1000.0, "to": null }
    ]
  }
}
```