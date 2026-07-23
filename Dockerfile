FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["src/symphony-test-1.Api/symphony-test-1.Api.csproj", "src/symphony-test-1.Api/"]
RUN dotnet restore "src/symphony-test-1.Api/symphony-test-1.Api.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/src/symphony-test-1.Api"
RUN dotnet build "symphony-test-1.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "symphony-test-1.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SymphonyTest1.Api.dll"]
