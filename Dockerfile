# Build stage - Use SDK image for restore, build, and publish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env
WORKDIR /app

# Copy solution file first :)
COPY *.sln ./

# Copy project files for dependency resolution
COPY API/*.csproj ./API/
COPY Core/*.csproj ./Core/
COPY Infrastructure/*.csproj ./Infrastructure/
COPY IntegrationTests/*.csproj ./IntegrationTests/
COPY TestUtilities/*.csproj ./TestUtilities/
COPY UnitTests/*.csproj ./UnitTests/

RUN dotnet restore

# Copy everything else and build
COPY . ./
WORKDIR /app/API

# Publish the application
RUN dotnet publish -c Release -o /app/out

# Runtime stage - Use lightweight runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

# Install postgresql client for health checks and migrations
RUN apt-get update && apt-get install -y postgresql-client && rm -rf /var/lib/apt/lists/*

# Copy published application
COPY --from=build-env /app/out .

# Create directory for bulk data
RUN mkdir -p /app/bulk-data

# Expose port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Integration

# Health check (the aspnet image has no curl/wget, so use bash's /dev/tcp)
HEALTHCHECK --interval=30s --timeout=10s --start-period=300s --retries=3 \
  CMD bash -c "exec 3<>/dev/tcp/127.0.0.1/8080 && printf 'GET /api/health HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n' >&3 && head -1 <&3 | grep -q 200"

ENTRYPOINT ["dotnet", "API.dll"]