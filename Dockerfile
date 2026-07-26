FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/symphony-test-1.Gateway/symphony-test-1.Gateway.csproj", "src/symphony-test-1.Gateway/"]
COPY ["src/symphony-test-1.ServiceDefaults/symphony-test-1.ServiceDefaults.csproj", "src/symphony-test-1.ServiceDefaults/"]
COPY ["src/symphony-test-1.Web/symphony-test-1.Web.csproj", "src/symphony-test-1.Web/"]
RUN dotnet restore "src/symphony-test-1.Gateway/symphony-test-1.Gateway.csproj"

COPY . .
WORKDIR "/src/src/symphony-test-1.Gateway"
RUN dotnet build "symphony-test-1.Gateway.csproj" -c Release -o /app/build --no-restore

FROM build AS publish
RUN dotnet publish "symphony-test-1.Gateway.csproj" -c Release -o /app/publish /p:UseAppHost=false --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080

RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SymphonyTest1.Gateway.dll"]
