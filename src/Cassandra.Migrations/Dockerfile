FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src

COPY src/. ./src/
RUN dotnet build "src/Cassandra.Migrations/Cassandra.Migrations.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "src/Cassandra.Migrations/Cassandra.Migrations.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Cassandra.Migrations.dll"]
