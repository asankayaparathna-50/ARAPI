# OpenAPI Project - Developer Guide

## Overview

This is a comprehensive developer guide for the OpenAPI project. This document maintains all essential information about the project structure, setup, architecture, and development practices.

## Project Summary

The OpenAPI project is a .NET-based REST API that integrates with SDMX (Statistical Data and Metadata eXchange) standards and Eurostat data sources. It provides endpoints for managing statistics, data libraries, and client applications.

---

## Table of Contents

1. [Project Structure](#project-structure)
2. [Technology Stack](#technology-stack)
3. [Prerequisites](#prerequisites)
4. [Getting Started](#getting-started)
5. [Architecture Overview](#architecture-overview)
6. [Key Features](#key-features)
7. [Development Guidelines](#development-guidelines)
8. [Database Setup](#database-setup)
9. [Configuration](#configuration)
10. [API Endpoints](#api-endpoints)
11. [Troubleshooting](#troubleshooting)
12. [Resources](#resources)

---

## Project Structure

### Solution Organization

```
OpenAPI/
├── src/
│   ├── OpenAPI.API/              # ASP.NET Core API Layer
│   ├── OpenAPI.Application/      # Business Logic & Services Layer
│   ├── OpenAPI.Domain/           # Domain Entities & Interfaces
│   ├── OpenAPI.Infrastructure/   # Data Access & Repository Layer
│   └── OpenAPI.sln              # Solution File
├── scripts/                      # SQL Scripts & Database Setup
├── docs/                         # Documentation
│   ├── development/             # Development Guides
│   ├── GUID/                    # GUID & SDMX Guides
│   └── Other/                   # Additional Documentation
└── deps/                         # Dependencies

```

### Layer Breakdown

#### 1. **OpenAPI.API** (Presentation Layer)
- **Controllers**: HTTP endpoint handlers
  - `AuthController.cs` - Authentication endpoints
  - `BaseController.cs` - Base controller with common functionality
  - `DataLibraryController.cs` - Data library management
  - `HomeController.cs` - Home/health check endpoints
  - `Examples/` - Example data controllers
  - `SDMX/` - SDMX-specific endpoints
  - `Statistics/` - Statistics-related endpoints
- **Filters**: Validation attributes for request processing
- **Helpers**: Utility functions (AppSettingsHelper, etc.)
- **Views**: MVC views (if applicable)
- **wwwroot**: Static files, CSS, JavaScript, Postman collections
- **Configuration**: appsettings.json, appsettings.development.json

#### 2. **OpenAPI.Application** (Business Logic Layer)
- **Services**: Core business logic
  - `ClientServices.cs` - Client management
  - `CommonServices.cs` - Shared utilities
  - `DataLibraryServices.cs` - Data library operations
  - `StatisticsServices.cs` - Statistics processing
  - `EstatSdmxMappingService.cs` - EUROSTAT to SDMX mapping
  - `EuristatSdmxTransformationService.cs` - Data transformation
  - `SdmxTransformationService.cs` - SDMX transformations
- **Extensions**: Helper extensions (SdmxConversionExtensions)

#### 3. **OpenAPI.Domain** (Domain Model Layer)
- **Entities**: Database models
  - Authentication entities
  - Data entities (DataCode, DataCodeListItem, DataValue)
  - Business entities (Client, Sector, Subject, Frequency)
  - Statistics entities
- **Interfaces**: Repository and service contracts
  - `IClientRepository.cs` - Client data access contracts

#### 4. **OpenAPI.Infrastructure** (Data Access Layer)
- **Repositories**: Data access implementations
- Implements repository pattern for database operations

---

## Technology Stack

- **Framework**: .NET (Version - specify in .csproj)
- **API**: ASP.NET Core
- **Language**: C# 9+
- **Database**: SQL Server (based on .sql scripts)
- **Integration**: SDMX, EUROSTAT   
- **Architecture Pattern**: Repository Pattern, Layered Architecture
- **Tools**: Visual Studio 2022+ or VS Code

---

## Prerequisites

- .NET SDK (check OpenAPI.sln for target framework)
- SQL Server (Express or higher)
- Visual Studio 2022 or VS Code with C# Dev Kit
- Git

### Installation Steps

1. **Install .NET SDK**
   ```bash
   # Download from https://dotnet.microsoft.com/download
   # Verify installation
   dotnet --version
   ```

2. **Install SQL Server**
   - Use SQL Server Express for development
   - Or use LocalDB

3. **Install Visual Studio 2022**
   - Select ASP.NET and web development workload
   - Select .NET desktop development

---

## Getting Started

### 1. Clone Repository
```bash
git clone [https://devops.cbsl.lk/DefaultCollection/_git/Open%20API]
cd OpenAPI
```

### 2. Restore Dependencies
```bash
cd src
dotnet restore
```

### 3. Database Setup
- Execute scripts in `scripts/` folder in SQL Server
- Order of execution:
  1. `20251014_new_SP.sql` - Latest stored procedures
  2. `SP_API.sql` - API-specific stored procedures
  3. `DataCode_*.sql` - Data code initialization
  4. `DataValue.sql` - Data value schema

### 4. Configure Application Settings
- Update `src/OpenAPI.API/appsettings.development.json`:
  ```json
  {
    "ConnectionStrings": {
      "DefaultConnection": "Server=NOPROD-DB;Database=CDWTEST;Trusted_Connection=True;MultipleActiveResultSets=True;TrustServerCertificate=True;"
    },
    "Logging": {
      "LogLevel": {
        "Default": "Information"
      }
    }
  }
  ```

### 5. Run the Application
```bash
cd src/OpenAPI.API
dotnet run
```

- API runs on: `https://localhost:5001` (HTTPS) or `http://localhost:5000` (HTTP)

### 6. Test with Postman
- Import `src/OpenAPI.API/wwwroot/OpenAPI.postman_collection.json` into Postman
- Use collection to test all endpoints

---

## Architecture Overview

### Layered Architecture Pattern

```
┌─────────────────────────────────────┐
│     OpenAPI.API (Controllers)       │  ← HTTP Requests
├─────────────────────────────────────┤
│   OpenAPI.Application (Services)    │  ← Business Logic
├─────────────────────────────────────┤
│    OpenAPI.Domain (Entities)        │  ← Data Models
├─────────────────────────────────────┤
│  OpenAPI.Infrastructure (Repos)     │  ← Data Access
├─────────────────────────────────────┤
│        SQL Server Database          │  ← Persistence
└─────────────────────────────────────┘
```

### Data Flow
1. **Request** → Controller (API)
2. **Processing** → Service (Application)
3. **Mapping** → Entity (Domain)
4. **Access** → Repository (Infrastructure)
5. **Storage** → Database

---

## Key Features

### Authentication & Authorization
- JWT-based authentication via `AuthController`
- Client application management
- Role-based access control

### Data Library Management
- Manage data sources and collections
- Data code and value management
- Data export functionality

### SDMX Integration
- EUROSTAT data source integration
- SDMX format transformation
- Statistical metadata exchange
- Mapping services between EUROSTAT and SDMX

### Statistics Processing
- Statistical data aggregation
- Query validation
- Complex data transformations

---

## Development Guidelines

### Code Organization
1. **Keep layers separate** - Don't reference UI code from business logic
2. **Use dependency injection** - Configure in `Extensions/DependencyInjection.cs`
3. **Follow SOLID principles** - Single responsibility, DRY code
4. **Use repositories** - All data access through repository pattern

### Naming Conventions
- **Classes**: PascalCase (e.g., `DataLibraryService`)
- **Methods**: PascalCase (e.g., `GetDataByCode()`)
- **Properties**: PascalCase (e.g., `DataCodeId`)
- **Private fields**: camelCase with underscore (e.g., `_logger`)
- **Constants**: UPPER_SNAKE_CASE (e.g., `MAX_PAGE_SIZE`)

### Adding New Features

#### Step 1: Create Domain Entity
```csharp
// In OpenAPI.Domain/Entities/
public class MyEntity
{
    public int Id { get; set; }
    public string Name { get; set; }
}
```

#### Step 2: Create Repository Interface
```csharp
// In OpenAPI.Domain/Interfaces/
public interface IMyRepository
{
    Task<MyEntity> GetById(int id);
    Task Add(MyEntity entity);
}
```

#### Step 3: Implement Repository
```csharp
// In OpenAPI.Infrastructure/Repositories/
public class MyRepository : IMyRepository
{
    // Implementation
}
```

#### Step 4: Create Service
```csharp
// In OpenAPI.Application/Services/
public class MyService
{
    private readonly IMyRepository _repository;
    
    public MyService(IMyRepository repository)
    {
        _repository = repository;
    }
}
```

#### Step 5: Create Controller
```csharp
// In OpenAPI.API/Controllers/
[ApiController]
[Route("api/[controller]")]
public class MyController : BaseController
{
    private readonly IMyService _service;
    
    public MyController(IMyService service)
    {
        _service = service;
    }
}
```

### Best Practices
- Add XML documentation comments to public methods
- Write unit tests for business logic
- Use async/await for I/O operations
- Validate input in controllers using `StatisticsQueryValidationAttribute`
- Log important operations and errors

---

## Database Setup

### Scripts Location
All SQL scripts are in `scripts/` directory.

### Execution Order
1. **20251014_new_SP.sql** - Latest updates
2. **SP_API.sql** - API procedures
3. **SP_Template.sql** - Template procedures (if needed)


### Database Schema
Main tables include:
- `DataCode` - Dimension codes
- `DataCodeListItem` - Code list items
- `DataValue` - Actual data values
- `ClientAppSetting` - Client configurations
- `Subject`, `Sector`, `Frequency` - Reference data

---

## Configuration

### appsettings.json
Main configuration file for production settings.

### appsettings.development.json
Development-specific overrides:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.\\SQLEXPRESS;Database=OpenAPIDb;Trusted_Connection=true;"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft": "Information"
    }
  },
  "AllowedHosts": "*"
}
```

### Environment Variables
Set in `Properties/launchSettings.json`:
- `ASPNETCORE_ENVIRONMENT`: Development, Staging, Production
- `ASPNETCORE_URLS`: API URLs

---

## API Endpoints

### Base URL
- Development: `http://localhost:5000` or `https://localhost:5001`

### Main Endpoint Groups

#### Authentication
- `POST /api/auth/login` - User login
- `POST /api/auth/logout` - User logout

#### Data Library
- `GET /api/datalibrary` - Get all data libraries
- `POST /api/datalibrary` - Create new library
- `GET /api/datalibrary/{id}` - Get specific library

#### Statistics
- `GET /api/statistics` - Get statistics
- `POST /api/statistics/query` - Execute query

#### SDMX
- `GET /api/sdmx/data` - Get SDMX formatted data
- `GET /api/sdmx/metadata` - Get SDMX metadata

*Note: Import Postman collection for complete endpoint documentation*

---

## Troubleshooting

### Common Issues

#### 1. Database Connection Failed
- **Problem**: "Cannot open database connection"
- **Solution**: 
  - Verify SQL Server is running
  - Check connection string in appsettings.development.json
  - Ensure database exists and scripts were executed

#### 2. Port Already in Use
- **Problem**: "Address already in use"
- **Solution**: 
  - Change port in launchSettings.json
  - Or kill process: `netstat -ano | findstr :5000` → `taskkill /PID [PID] /F`

#### 3. NuGet Package Restore Failed
- **Problem**: "Unable to resolve dependencies"
- **Solution**: 
  ```bash
  dotnet nuget locals all --clear
  dotnet restore
  ```

#### 4. Build Errors
- **Problem**: Compilation errors
- **Solution**: 
  - Clean solution: `dotnet clean`
  - Rebuild: `dotnet build`

---

## Resources

### Documentation
- [SDMX Quickstart](../GUID/SDMX_QUICKSTART.md)
- [SDMX Integration Guide](../GUID/SDMX_INTEGRATION_GUIDE.md)
- [EUROSTAT Integration](../GUID/SDMX_EUROSTAT_PACKAGES_GUIDE.md)
- [GUID Implementation](../GUID/IMPLEMENTATION_SUMMARY.md)
- [Update Endpoints Guide](../GUID/SDMX_UPDATE_ENDPOINTS.md)

### External Resources
- [ASP.NET Core Documentation](https://docs.microsoft.com/en-us/aspnet/core/)
- [Entity Framework Core](https://docs.microsoft.com/en-us/ef/core/)
- [SDMX Standard](https://sdmx.org/)
- [EUROSTAT API](https://ec.europa.eu/eurostat/web/main/home)
- [REST API Best Practices](https://restfulapi.net/)

### Useful Commands

#### Build & Run
```bash
# Restore dependencies
dotnet restore

# Build solution
dotnet build

# Run project
dotnet run

# Run with specific environment
dotnet run --configuration Debug

# Publish for production
dotnet publish -c Release -o ./publish
```

#### Database
```sql
-- Connect to database
-- Execute all scripts in order
-- Verify with: SELECT * FROM sys.objects WHERE type='U'
```

---

## Maintenance & Updates

This guide should be updated whenever:
- New controllers or services are added
- Architecture changes are made
- New features are implemented
- Setup procedures change
- Dependencies are updated

**Last Updated**: [Update with current date when changes are made]

---

## Contact & Support

For questions or issues, refer to:
- Project documentation in `/docs`
- SDMX guides in `/docs/GUID`
- Code comments in source files

---

**Version**: 1.0  
**Created**: 2026-07-03
**CreatedBy**:asankayapa@cbsl.lk
