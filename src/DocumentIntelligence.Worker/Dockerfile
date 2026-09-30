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
COPY src/DocumentIntelligence.Worker/DocumentIntelligence.Worker.csproj src/DocumentIntelligence.Worker/
RUN dotnet restore src/DocumentIntelligence.Worker/DocumentIntelligence.Worker.csproj

COPY src/ src/
RUN dotnet publish src/DocumentIntelligence.Worker/DocumentIntelligence.Worker.csproj \
    --configuration Release --no-restore --output /app -p:UseAppHost=false

# The ASP.NET Core runtime, not the plain one: Infrastructure uses Identity and ServiceDefaults health checks.
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

USER $APP_UID
ENTRYPOINT ["dotnet", "DocumentIntelligence.Worker.dll"]
