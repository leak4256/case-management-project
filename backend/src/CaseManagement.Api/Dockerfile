# Build context is backend/, not this folder: the API references three sibling projects.
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /source

COPY Directory.Build.props ./
COPY src/CaseManagement.Domain/CaseManagement.Domain.csproj src/CaseManagement.Domain/
COPY src/CaseManagement.Application/CaseManagement.Application.csproj src/CaseManagement.Application/
COPY src/CaseManagement.Infrastructure/CaseManagement.Infrastructure.csproj src/CaseManagement.Infrastructure/
COPY src/CaseManagement.Api/CaseManagement.Api.csproj src/CaseManagement.Api/
RUN dotnet restore src/CaseManagement.Api/CaseManagement.Api.csproj

COPY src/ src/
RUN dotnet publish src/CaseManagement.Api/CaseManagement.Api.csproj -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

# The runtime image ships with neither curl nor wget, and the Compose healthcheck needs one.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app ./
EXPOSE 8080
ENTRYPOINT ["dotnet", "CaseManagement.Api.dll"]
