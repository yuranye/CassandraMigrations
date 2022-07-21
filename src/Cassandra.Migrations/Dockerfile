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

COPY build/. .

#install cqlsh
RUN apt-get update && apt-get install -y python3-pip
RUN pip install -U cqlsh

RUN chmod +x ./docker-entrypoint.sh

ENTRYPOINT ["./docker-entrypoint.sh"]
