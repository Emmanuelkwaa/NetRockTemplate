# Blazor SSR frontend. The build context is ./src, because the project
# references backend/NetRockTemplate.ServiceDefaults.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
# curl is used by the Docker healthcheck probe
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Backend config - needed to restore NetRockTemplate.ServiceDefaults (project reference)
COPY ["backend/Directory.Build.props", "backend/"]
COPY ["backend/Directory.Packages.props", "backend/"]
COPY ["backend/nuget.config", "backend/"]

# Copy csproj files first for layer-cached restore
COPY ["backend/NetRockTemplate.ServiceDefaults/NetRockTemplate.ServiceDefaults.csproj", "backend/NetRockTemplate.ServiceDefaults/"]
COPY ["frontend/NetRockTemplate.UI/NetRockTemplate.UI.csproj", "frontend/NetRockTemplate.UI/"]

RUN dotnet restore "frontend/NetRockTemplate.UI/NetRockTemplate.UI.csproj"

# Copy source
COPY backend/NetRockTemplate.ServiceDefaults/ backend/NetRockTemplate.ServiceDefaults/
COPY frontend/NetRockTemplate.UI/ frontend/NetRockTemplate.UI/

WORKDIR "/src/frontend/NetRockTemplate.UI"
RUN dotnet build "NetRockTemplate.UI.csproj" -c Release

FROM build AS publish

# StripDevConfig controls whether appsettings.Development.json is excluded.
# Default: true (production). Pass STRIP_DEV_CONFIG=false as a build arg to keep it.
ARG STRIP_DEV_CONFIG=true

RUN dotnet publish "NetRockTemplate.UI.csproj" -c Release -o /app/publish \
    /p:UseAppHost=false /p:DebugType=None /p:DebugSymbols=false

# Defense-in-depth: strip non-production config
RUN if [ "$STRIP_DEV_CONFIG" = "true" ]; then \
        rm -f /app/publish/appsettings.Development.json; \
    fi

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

EXPOSE 8080
USER $APP_UID

ENTRYPOINT ["dotnet", "NetRockTemplate.UI.dll"]
