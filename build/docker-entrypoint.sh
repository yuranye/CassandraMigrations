#!/bin/sh

set -e

host="$(getopt -q -u -l "host:" "h:" "$@" | grep -oP "(?<= --host | -h ).*(?= --.*)" || printf ".")"
port="$(getopt -q -u -l "port:" "$@" | grep -oP "(?<= --port ).*(?= --.*)" || printf ".")"

echo "Waiting for cassandra to be ready..."

while ! cqlsh "$host" "$port" -e 'describe cluster'; do
  sleep 3
done

exec dotnet Cassandra.Migrations.dll "$@"
