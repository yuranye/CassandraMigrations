#!/bin/sh

set -e

host="$(getopt -q -u -l "host:" "h:" "$@" | grep -oP "(?<= --host | -h ).*(?= --.*)" || printf ".")"
port="$(getopt -q -u -l "port:" "$@" | grep -oP "(?<= --port ).*(?= --.*)" || printf ".")"

while ! cqlsh "$host" "$port" -e 'describe cluster' ; do
    sleep 3
    echo "waiting for cassandra"
done

exec dotnet Cassandra.Migrations.dll "$@"