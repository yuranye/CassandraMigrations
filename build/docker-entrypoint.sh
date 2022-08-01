#!/bin/bash

set -e

host="$(getopt -q -u -l "host:" "h:" "$@" | grep -oP "(?<= --host | -h ).*(?= --.*)" || printf ".")"
port="$(getopt -q -u -l "port:" "$@" | grep -oP "(?<= --port ).*(?= --.*)" || printf ".")"

echo "Waiting for cassandra to be ready..."

max_retries=5

for ((i = 1; i <= max_retries; i++)); do
  if ! cqlsh "$host" "$port" -e 'describe cluster'; then
    echo "Retrying: $i"
    sleep 5
  else
    exec dotnet Cassandra.Migrations.dll "$@"
  fi
done

echo "All cassandra connection retries failed"
exit 1
