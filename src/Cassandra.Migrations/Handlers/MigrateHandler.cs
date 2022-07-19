using System.CommandLine;
using Polly;
using Polly.Fallback;
using Spectre.Console;

namespace Cassandra.Migrations.Handlers;

public class MigrateCommandHandler
{
    public static Command Build()
    {
        var host = new Option<string>("--host", description: "The Apache Cassandra cluster host") { IsRequired = true };
        host.AddAlias("-h");

        var port = new Option<int>("--port", description: "The Apache Cassandra cluster custom port",
            getDefaultValue: () => 9042) { IsRequired = false };

        var keyspace = new Option<string>("--keyspace", description: "The Apache Cassandra keyspace")
            { IsRequired = true };
        keyspace.AddAlias("-k");

        var user = new Option<string>("--user", description: "The Apache Cassandra user");
        user.AddAlias("-u");

        var password = new Option<string>("--password", description: "The Apache Cassandra password");
        password.AddAlias("-p");

        var scripts = new Option<string>("--scripts",
            description: "Folder path which contains scripts of the migrations") { IsRequired = true };
        scripts.AddAlias("-s");

        var forceKeyspace = new Option<bool>("--force_keyspace",
            description: "Forces keyspace creation with SimpleStrategy and replication factor: 3",
            getDefaultValue: () => false);

        var migrateCommand = new Command("migrate") { host, keyspace, user, password, scripts, port, forceKeyspace };

        migrateCommand.SetHandler(
            async (hostOption, keyspaceOption, userOption, passwordOption, scriptsOption, portOption,
                forceKeyspaceOption) =>
            {
                await Handle(hostOption, keyspaceOption, userOption, passwordOption, scriptsOption, portOption,
                    forceKeyspaceOption);
            }, host, keyspace, user, password, scripts, port, forceKeyspace);

        return migrateCommand;
    }

    private static async Task Handle(string host, string keyspace, string user, string password,
        string scriptsPath, int port, bool forceKeyspace)
    {
        ISession session = null;
        var migrations = new List<(Migration Migrations, bool Applied)>();

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Arc)
            .SpinnerStyle(Style.Parse("yellow"))
            .StartAsync("Starting migrations...", async statusContext =>
            {
                AnsiConsole.MarkupLine($"Connecting to host - {host} | Port: {port}...");
                statusContext.Status("Connecting to Apache Cassandra");

                var clusterBuilder = Cluster.Builder()
                    .AddContactPoint($"{host}")
                    .WithPort(port)
                    .WithRetryPolicy(new MigrationsRetryPolicy(5, 5, 5));

                if (!string.IsNullOrWhiteSpace(user) && !string.IsNullOrWhiteSpace(password))
                {
                    AnsiConsole.MarkupLine($"Using credentials: user - {user} | password: {password}");
                    clusterBuilder.WithCredentials(user, password);
                }

                var cluster = clusterBuilder.Build();

                var fallbackPolicy = Policy<ISession>.Handle<NoHostAvailableException>()
                    .FallbackAsync
                        (fallbackAction: _ => Task.FromResult<ISession>(null));

                var retryPolicy = Policy<ISession>
                    .Handle<NoHostAvailableException>()
                    .WaitAndRetryAsync(Constants.RetryCount, attempt => TimeSpan.FromSeconds(attempt * 3),
                        onRetry: (_, _, attempt, _) =>
                        {
                            AnsiConsole.MarkupLine("Connection failed: no hosts available, retrying ...");
                            statusContext.Status($"Reconnect attempt: {attempt}");
                        });

                session = await Policy.WrapAsync(fallbackPolicy, retryPolicy)
                    .ExecuteAsync(async () => await cluster.ConnectAsync());

                if (session == null)
                {
                    statusContext.Status("Migration failed!");
                    statusContext.Spinner(Spinner.Known.Toggle9);
                    statusContext.SpinnerStyle(new Style(foreground: Color.Red));
                    statusContext.Refresh();
                    
                    AnsiConsole.MarkupLine("Cannot connect to Apache Cassandra, exiting");
                    Environment.Exit(1);
                }
                
                statusContext.Status("Connected!");

                if (forceKeyspace)
                {
                    AnsiConsole.MarkupLine($"Enforcing keyspace - {keyspace} with simple strategy ...");
                    session.CreateKeyspaceIfNotExists(keyspace, new Dictionary<string, string>
                    {
                        { "class", "SimpleStrategy" },
                        { "replication_factor", "3" },
                    });
                }

                session.ChangeKeyspace(keyspace);

                statusContext.Spinner(Spinner.Known.Dots);
                statusContext.Status("Reading migrations...");

                var migrationsReader = new MigrationsReader(keyspace, scriptsPath);

                var appliedMigrations = await migrationsReader.ReadAppliedMigrations(session);

                migrations = await migrationsReader
                    .ReadMigrationScripts(appliedMigrations.Select(m => m.Name).ToHashSet())
                    .ToListAsync();
            });

        var cliTable = new Table().LeftAligned();

        await AnsiConsole.Live(cliTable)
            .Overflow(VerticalOverflow.Ellipsis)
            .Cropping(VerticalOverflowCropping.Bottom)
            .StartAsync(async tableContext =>
            {
                var stateFailed = false;

                cliTable.AddColumn("Version");
                cliTable.AddColumn("Name");
                cliTable.AddColumn("Status");
                tableContext.Refresh();

                foreach (var (migration, applied) in migrations)
                {
                    if (stateFailed)
                    {
                        cliTable.AddRow($"[blue]{migration.Version}[/]", migration.Name,
                            $"{Emoji.Known.BrownCircle} Skipped");
                    }
                    else if (applied)
                    {
                        cliTable.AddRow($"[blue]{migration.Version}[/]", migration.Name,
                            $"[green]{Emoji.Known.CheckMark}[/] Applied");
                    }
                    else
                    {
                        try
                        {
                            cliTable.AddRow($"[blue]{migration.Version}[/]", migration.Name,
                                $"{Emoji.Known.YellowCircle} In progress");
                            tableContext.Refresh();

                            await session.ExecuteAsync(new SimpleStatement(migration.Cql));
                            await session.ExecuteAsync(new SimpleStatement(
                                "INSERT INTO schema_migrations (version, cql, name, time) VALUES (?, ?, ?, toTimestamp(now()));",
                                migration.Version, migration.Cql, migration.Name));

                            cliTable.RemoveRow(cliTable.Rows.Count - 1);
                            cliTable.AddRow($"[blue]{migration.Version}[/]", migration.Name,
                                $"[green]{Emoji.Known.CheckMark}[/] Applied");
                        }
                        catch
                        {
                            cliTable.RemoveRow(cliTable.Rows.Count - 1);
                            cliTable.AddRow($"[blue]{migration.Version}[/]", migration.Name,
                                $"{Emoji.Known.RedCircle} Failed");

                            stateFailed = true;
                        }
                    }

                    tableContext.Refresh();
                }
            });
    }
}