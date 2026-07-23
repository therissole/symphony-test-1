FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/symphony-test-1.Api/symphony-test-1.Api.csproj", "src/symphony-test-1.Api/"]
RUN dotnet restore "src/symphony-test-1.Api/symphony-test-1.Api.csproj"

COPY . .
WORKDIR "/src/src/symphony-test-1.Api"
RUN dotnet build "symphony-test-1.Api.csproj" -c Release -o /app/build --no-restore

FROM build AS publish
RUN dotnet publish "symphony-test-1.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SymphonyTest1.Api.dll"]
