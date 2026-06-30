# ASP.NET Core Boilerplate

A production-ready ASP.NET Core 9 boilerplate with Clean Architecture, JWT authentication, RBAC authorization, email, blob storage, real-time notifications, and structured logging.

---

## Tech Stack

| Layer | Technology |
|---|---|
| Framework | ASP.NET Core 9, EF Core 8 |
| Database | SQL Server |
| Auth | JWT Bearer (access + refresh tokens with revocation) |
| Authorization | RBAC — policy-based with `[HasPermission]` attribute |
| Email | Azure Communication Services |
| Blob Storage | Azure Blob Storage |
| Real-time | SignalR |
| Caching | Redis + In-Memory |
| Logging | Serilog (file rolling + console) |
| Validation | FluentValidation |
| Mapping | AutoMapper |
| API Docs | OpenAPI (.NET native) + Scalar |
| Rate Limiting | ASP.NET built-in sliding window |

---

## Project Structure

```
/
├── API/                          # Entry point — controllers, middleware, config
│   ├── Controllers/
│   │   ├── BaseApiController.cs  # Maps ApiResponse<T> to correct HTTP status codes
│   │   └── v1/                   # Versioned API endpoints
│   ├── Configurations/
│   │   └── ServiceExtensions.cs  # DI registration
│   ├── appsettings.example.json  # Template — copy and fill in values
│   └── Program.cs
│
├── Core/               # Interfaces, DTOs, domain logic (no external dependencies)
│   ├── Authorization/            # HasPermission attribute + RBAC handler
│   ├── Common/                   # PermissionCodes, ICurrentUser, LoggedInUser
│   ├── Interfaces/               # Service and repository contracts
│   ├── Middlewares/              # Exception handling, unauthorized handler
│   └── ViewModel/                # Request/Response DTOs
│
├── DomainPersistence/  # EF Core entities, DbContext, migrations
│   └── Entities/
│
└── Infrastructure/     # Implementations of all interfaces
    ├── Auth/                     # AuthService, JWT setup
    ├── Email/                    # EmailService (Azure Communication)
    ├── Services/                 # UserService, RoleService, BlobService, etc.
    ├── Database/                 # GenericRepository, UnitOfWork
    └── Notification/             # NotificationService, SignalR hub
```

---

## Getting Started

### 1. Configure secrets

Copy the example config and fill in your values:

```bash
cp API/appsettings.example.json API/appsettings.json
```

Edit `appsettings.json` and set:
- `ConnectionStrings:DefaultConnection` — SQL Server connection string
- `Authentication:Jwt:JwtSecretKey` — At least 32 random characters
- `Authentication:Jwt:Issuer` and `Audience` — your API and frontend URLs
- `AzureStorage:BlobConnectionString` — Azure Blob connection string
- `AzureCommunicationServiceConfig:COMMUNICATION_SERVICES_CONNECTION_STRING`
- `AppName` — Your application name (used in email templates)
- `FrontendUrl` — Used in password reset links

> **`appsettings.json` is git-ignored.** Never commit real credentials.

### 2. Apply database migrations

```bash
cd API
dotnet ef database update
```

### 3. Run

```bash
dotnet run --project API
```

Scalar API reference: `https://localhost:{port}/scalar` — OpenAPI document at `/openapi/v1.json`

---

## API Endpoints

### Auth — `POST /api/v1/auth/...`

| Endpoint | Auth | Rate Limited | Description |
|---|---|---|---|
| `login` | Public | Yes | Email + password → access + refresh token |
| `refresh` | Public | Yes | Rotate refresh token |
| `logout` | Required | No | Revoke refresh token (send `refreshToken` in body) |
| `forgot-password` | Public | Yes | Send password reset link to email |
| `reset-password` | Public | Yes | Set new password using reset token |
| `verify-otp` | Public | Yes | Verify email OTP, returns tokens |
| `resend-otp` | Public | Yes | Resend OTP to email |
| `validate-token/{token}` | Public | No | Validate an access token |
| `validate-reset-password-token` | Public | No | Validate a password reset token |

### Users — `/api/v1/users`

| Method | Endpoint | Permission |
|---|---|---|
| GET | `/` | `Users.View` |
| GET | `/{id}` | `Users.View` |
| POST | `/` | `Users.Create` |
| PUT | `/{id}` | `Users.Update` |
| DELETE | `/{id}` | `Users.Delete` |
| POST | `/{id}/change-password` | Self or `Users.Update` |

### Roles — `/api/v1/roles`

Requires `Roles.Manage` (write) or `Roles.View` (read).

### Permissions — `/api/v1/permissions`

Read: authenticated. Create: `Roles.Manage`.

### Notifications — `/api/v1/notifications`

All endpoints require authentication. Scoped to current user.

### Upload — `POST /api/v1/upload/image`

Upload base64 image → returns Azure Blob SAS URL.

---

## Key Design Decisions

### HTTP Status Codes

Services return `ApiResponse<T>` with a `StatusCode` property. `BaseApiController.ToResponse<T>()` maps it to the correct HTTP status:

| Factory method | HTTP |
|---|---|
| `SuccessResponse` | 200 |
| `ErrorResponse` | 400 |
| `UnauthorizedResponse` | 401 |
| `ForbiddenResponse` | 403 |
| `NotFoundResponse` | 404 |
| `ConflictResponse` | 409 |
| `ServerErrorResponse` | 500 |

### Permission-Based Authorization

Permissions are embedded as `permission` claims in the JWT access token at login. The `[HasPermission("Users.Create")]` attribute validates claims in-memory — no DB round-trip per request.

Adding a new permission:
1. Add constant to `PermissionCodes.cs`
2. Done — policies are registered dynamically via reflection

### Refresh Token Revocation

Refresh tokens are tracked in `UserRefreshTokens` table by JWT ID (JTI). On refresh: old token is revoked + new token issued (rotation). On logout: token is explicitly revoked.

### Rate Limiting

Auth endpoints: **10 requests / 60 seconds** sliding window per IP. Returns `429 Too Many Requests` when exceeded.

### Exception Handling

`ExceptionHandlingMiddleware` catches all unhandled exceptions:
- Logs via Serilog + persists to `SystemErrorLogs` DB table
- **Development**: exception message included in response
- **Production**: generic message only — no internal details leaked

### BlobServiceClient

Registered as a **singleton** in DI. The underlying `BlobServiceClient` is thread-safe and designed for reuse — not created per request.

---

## Adding Business Features

1. Add entity to `DomainPersistence/Entities/`
2. Add `DbSet<T>` to `ApplicationDBContext`
3. Register repo in `IUnitOfWork` and `UnitOfWork`
4. Create service interface in `Core/Interfaces/Services/`
5. Implement in `Infrastructure/Services/`
6. Register in `ServiceExtensions.cs`
7. Add controller extending `BaseApiController`
8. Run `dotnet ef migrations add YourFeature`

---

## Environment Variables

All secrets in `appsettings.json` (git-ignored). In production use environment variables with `__` as separator:

```
ConnectionStrings__DefaultConnection=...
Authentication__Jwt__JwtSecretKey=...
AzureStorage__BlobConnectionString=...
```

For Azure: use Key Vault references in App Service configuration.
