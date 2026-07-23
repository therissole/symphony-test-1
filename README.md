# C# AI-Enabled SDLC Starter Kit

A comprehensive starter project for C# applications that demonstrates best practices for modern software development with AI-assisted workflows.

## Features

- ✅ **ASP.NET Core** - Modern web API with minimal APIs
- ✅ **Vertical Slice Architecture** - Feature-based code organization (Jimmy Bogard pattern)
- ✅ **PostgreSQL** - Enterprise-grade relational database
- ✅ **Flyway** - Automated database migrations
- ✅ **Dapper** - Lightweight, high-performance ORM
- ✅ **Docker** - Full containerization with docker-compose
- ✅ **NUnit** - Unit, integration, and E2E test suites
- ✅ **Testcontainers** - Real database testing
- ✅ **Serilog** - Structured logging
- ✅ **OpenTelemetry** - Distributed tracing and observability
- ✅ **Health Checks** - Database connectivity monitoring
- ✅ **Feature Flags** - Microsoft Feature Management
- ✅ **GitHub Actions** - Complete CI/CD pipeline
- ✅ **Mintlify** - Beautiful documentation

## Quick Start

### Using Docker (Recommended)

```bash
# Clone the repository
git clone https://github.com/therissole/symphony-test-1.git
cd symphony-test-1

# Start all services
docker-compose up -d

# Wait for services to initialize (~30 seconds)
# Check the API health
curl http://localhost:8080/api/health
```

The application will be available at http://localhost:8080

### Local Development

```bash
# Restore dependencies
dotnet restore

# Run tests
dotnet test

# Run the API (requires PostgreSQL running)
cd src/symphony-test-1.Api
dotnet run
```

## Project Structure

```
.
├── src/
│   └── symphony-test-1.Api/           # Main API application
│       ├── Features/              # Vertical slice features
│       │   ├── Languages/         # Language CRUD operations
│       │   ├── Greetings/        # Greeting CRUD operations
│       │   └── Health/           # Health check endpoint
│       └── Infrastructure/        # Shared infrastructure
├── tests/
│   ├── unit/                     # Unit tests
│   ├── integration/              # Integration tests
│   └── e2e/                      # End-to-end tests
├── db/
│   └── migrations/               # Flyway SQL migrations
├── docs/                         # Mintlify documentation
├── .github/
│   └── workflows/                # CI/CD pipelines
├── Dockerfile                    # API container definition
└── docker-compose.yml            # Multi-container orchestration
```

## API Endpoints

### Health
- `GET /api/health` - Check application and database health

### Languages
- `GET /api/languages` - List all languages
- `GET /api/languages/{id}` - Get language by ID
- `POST /api/languages` - Create new language
- `PUT /api/languages/{id}` - Update language
- `DELETE /api/languages/{id}` - Delete language

### Greetings
- `GET /api/greetings` - List all greetings
- `GET /api/greetings/{id}` - Get greeting by ID
- `GET /api/greetings/by-language/{code}` - Get greeting by language code
- `POST /api/greetings` - Create new greeting
- `PUT /api/greetings/{id}` - Update greeting
- `DELETE /api/greetings/{id}` - Delete greeting

## Example Usage

```bash
# List all languages
curl http://localhost:8080/api/languages

# Get a greeting in English
curl http://localhost:8080/api/greetings/by-language/en

# Get a formal greeting in Spanish
curl "http://localhost:8080/api/greetings/by-language/es?formal=true"

# Create a new language
curl -X POST http://localhost:8080/api/languages \
  -H "Content-Type: application/json" \
  -d '{"name": "French", "code": "fr"}'
```

## Testing

The project includes three levels of testing:

```bash
# Run all tests
dotnet test

# Run specific test suites
dotnet test tests/unit/symphony-test-1.UnitTests/
dotnet test tests/integration/symphony-test-1.IntegrationTests/
dotnet test tests/e2e/symphony-test-1.E2ETests/
```

### Test Coverage
- **Unit Tests** - Component isolation with mocking
- **Integration Tests** - Real database operations with Testcontainers
- **E2E Tests** - Complete API workflows

## Architecture

This project follows **Vertical Slice Architecture**:

- Code is organized by **features** rather than technical layers
- Each feature is self-contained with its models, repository, and endpoints
- Reduces coupling between features
- Makes features easier to find, understand, and modify

Learn more in the [documentation](docs/architecture.mdx).

## Database

- **PostgreSQL 16** with Alpine Linux
- **UUID primary keys** for distributed systems
- **Flyway migrations** for version control
- **Referential integrity** with foreign key constraints
- **Cascade deletes** for data consistency

## Observability

- **Serilog** - Structured logging with context enrichment
- **OpenTelemetry** - Distributed tracing for HTTP and database
- **Health Checks** - Liveness and readiness probes

## CI/CD

GitHub Actions workflow includes:
- Build and compile
- Unit tests
- Integration tests with PostgreSQL service
- E2E tests in containerized environment
- Docker image build
- Container orchestration testing

## Documentation

Full documentation is available in the `docs/` directory:
- Getting Started Guide
- Architecture Overview
- Feature Documentation
- API Reference

## Contributing

This is a starter kit template. Feel free to:
- Fork and customize for your needs
- Add new features following the vertical slice pattern
- Extend the test suites
- Improve documentation

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- **Vertical Slice Architecture** - Inspired by Jimmy Bogard
- **Minimal APIs** - ASP.NET Core team
- **Testcontainers** - Testcontainers community

---

**Ready to build AI-enabled applications with modern C# practices!**
