FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app
#install cqlsh
RUN apt-get update && apt-get install -y python3-pip
RUN pip install -U cqlsh

#entrypoint wrapper
COPY build/. .
RUN chmod +x ./docker-entrypoint.sh

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR src
COPY src/. ./

RUN dotnet restore "Cassandra.Migrations/Cassandra.Migrations.csproj"
RUN dotnet build "Cassandra.Migrations/Cassandra.Migrations.csproj" -c Release --no-restore -o /app/build

FROM build AS publish
RUN dotnet publish "Cassandra.Migrations/Cassandra.Migrations.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["./docker-entrypoint.sh"]
