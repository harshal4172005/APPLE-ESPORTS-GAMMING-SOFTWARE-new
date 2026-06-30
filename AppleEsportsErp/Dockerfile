# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore as distinct layers
COPY ["src/AppleEsportsErp.Api/AppleEsportsErp.Api.csproj", "AppleEsportsErp.Api/"]
COPY ["src/AppleEsportsErp.Application/AppleEsportsErp.Application.csproj", "AppleEsportsErp.Application/"]
COPY ["src/AppleEsportsErp.Domain/AppleEsportsErp.Domain.csproj", "AppleEsportsErp.Domain/"]
COPY ["src/AppleEsportsErp.Infrastructure/AppleEsportsErp.Infrastructure.csproj", "AppleEsportsErp.Infrastructure/"]

RUN dotnet restore "AppleEsportsErp.Api/AppleEsportsErp.Api.csproj"

# Copy everything else and build
COPY src/ ./
WORKDIR "/src/AppleEsportsErp.Api"
RUN dotnet build "AppleEsportsErp.Api.csproj" -c Release -o /app/build
RUN dotnet publish "AppleEsportsErp.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime Stage (using Alpine for smallest footprint)
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app
COPY --from=build /app/publish .

# Expose API port
EXPOSE 8080

ENTRYPOINT ["dotnet", "AppleEsportsErp.Api.dll"]
