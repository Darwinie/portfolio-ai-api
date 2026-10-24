# syntax=docker/dockerfile:1
# Multi-stage build for the Portfolio.AI.Api ASP.NET Core (.NET 10) Web API.
# Build context is this directory (the solution root).

# ---------- Build stage ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore separately so the NuGet layer is cached until the project file changes.
COPY Portfolio.AI.Api/Portfolio.AI.Api.csproj Portfolio.AI.Api/
RUN dotnet restore Portfolio.AI.Api/Portfolio.AI.Api.csproj

# Copy the API project only. Tests, .vs, .agents, local settings, and secrets are
# excluded by .dockerignore, so nothing sensitive enters the build context.
COPY Portfolio.AI.Api/ Portfolio.AI.Api/

RUN dotnet publish Portfolio.AI.Api/Portfolio.AI.Api.csproj \
        --configuration Release \
        --no-restore \
        --output /app/publish \
        /p:UseAppHost=false

# ---------- Runtime stage ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

# The published output includes Data/portfolio-knowledge.json and
# Data/portfolio-profile-public.md (see the csproj), which the app resolves from the
# content root. The content root is the working directory, so keep them at /app/Data.
WORKDIR /app

# Cloud Run sets PORT at runtime and the app binds to it (see Program.cs). This default
# keeps `docker run` working without PORT and matches Cloud Run's default port.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .

# The .NET 10 images define APP_UID (1654) for a built-in non-root user. Cloud Run also
# supports running as an arbitrary UID, so the app must not require a fixed user.
USER $APP_UID

ENTRYPOINT ["dotnet", "Portfolio.AI.Api.dll"]
