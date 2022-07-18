using System.CommandLine;
using Cassandra.Migrations.Handlers;
using Spectre.Console;

AnsiConsole.Write(
    new FigletText("Cassandra Migrations")
        .Centered()
        .Color(Color.Fuchsia));

var rootCommand = new RootCommand("The tool to apply Apache Cassandra schema migrations")
    { MigrateCommandHandler.Build() };

return await rootCommand.InvokeAsync(args);
