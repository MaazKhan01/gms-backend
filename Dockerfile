# Multi-stage build for the GMS API (API + Core + Infrastructure + DomainPersistence).
# Bypasses Railway's auto-detection (Railpack can't find a .csproj at the build
# root since all projects live in subdirectories) by building explicitly against
# API/API.csproj, whose relative ProjectReference paths resolve correctly here
# because the whole repo is copied into the build context.

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy just the project files first so `dotnet restore` is cached across builds
# unless a .csproj actually changes (much faster rebuilds when only .cs files change).
COPY API/API.csproj API/
COPY Core/Core.csproj Core/
COPY Infrastructure/Infrastructure.csproj Infrastructure/
COPY DomainPersistence/DomainPersistence.csproj DomainPersistence/
RUN dotnet restore API/API.csproj

# Now copy the rest of the source and publish.
COPY . .
RUN dotnet publish API/API.csproj -c Release -o /app/publish --no-restore

# ---- Runtime image ----
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Railway assigns a random $PORT at runtime; Kestrel must bind to it. TLS is
# terminated at Railway's edge, so the container only ever needs to speak HTTP.
ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} dotnet API.dll"]
