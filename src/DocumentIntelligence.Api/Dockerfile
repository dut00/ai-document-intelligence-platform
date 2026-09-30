# syntax=docker/dockerfile:1
# Build context: the repository root (see docker-compose.yml, profile "full").

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

# Restore from the project files alone, so this layer is reused until a dependency changes.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/DocumentIntelligence.Domain/DocumentIntelligence.Domain.csproj src/DocumentIntelligence.Domain/
COPY src/DocumentIntelligence.Application/DocumentIntelligence.Application.csproj src/DocumentIntelligence.Application/
COPY src/DocumentIntelligence.Contracts/DocumentIntelligence.Contracts.csproj src/DocumentIntelligence.Contracts/
COPY src/DocumentIntelligence.Infrastructure/DocumentIntelligence.Infrastructure.csproj src/DocumentIntelligence.Infrastructure/
COPY src/DocumentIntelligence.ServiceDefaults/DocumentIntelligence.ServiceDefaults.csproj src/DocumentIntelligence.ServiceDefaults/
COPY src/DocumentIntelligence.Api/DocumentIntelligence.Api.csproj src/DocumentIntelligence.Api/
RUN dotnet restore src/DocumentIntelligence.Api/DocumentIntelligence.Api.csproj

COPY src/ src/
RUN dotnet publish src/DocumentIntelligence.Api/DocumentIntelligence.Api.csproj \
    --configuration Release --no-restore --output /app -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

# The image has no curl; bash can speak just enough HTTP to ask the liveness endpoint.
HEALTHCHECK --interval=5s --timeout=3s --start-period=30s --retries=10 \
    CMD ["bash", "-c", "exec 3<>/dev/tcp/127.0.0.1/8080 && printf 'GET /health/live HTTP/1.1\\r\\nHost: localhost\\r\\nConnection: close\\r\\n\\r\\n' >&3 && head -n 1 <&3 | grep -q ' 200 '"]

USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "DocumentIntelligence.Api.dll"]
