using System.CommandLine;
using Cassandra.Mapping;
using Spectre.Console;

namespace Cassandra.Migrations.Handlers;

public class MigrateCommandHandler
{
    public static Command Build()
    {
        var host = new Option<string>("--host", description: "The Apache Cassandra cluster host") { IsRequired = true };
        host.AddAlias("-h");

        var keyspace = new Option<string>("--keyspace", description: "The Apache Cassandra keyspace"){ IsRequired = true };
        keyspace.AddAlias("-k");

        var user = new Option<string>("--user", description: "The Apache Cassandra user");
        user.AddAlias("-u");

        var password = new Option<string>("--password", description: "The Apache Cassandra password");
        password.AddAlias("-p");

        var scripts = new Option<string>("--scripts",
            description: "Folder path which contains scripts of the migrations"){ IsRequired = true };
        scripts.AddAlias("-s");

        var migrateCommand = new Command("migrate") { host, keyspace, user, password, scripts };

        migrateCommand.SetHandler(
            async (hostOption, keyspaceOption, userOption, passwordOption, scriptsOption) =>
            {
                await Handle(hostOption, keyspaceOption, userOption, passwordOption, scriptsOption);
            }, host, keyspace, user, password, scripts);

        return migrateCommand;
    }

    private static async Task Handle(string host, string keyspace, string user, string password,
        string scriptsPath)
    {
        var migrationsReader = new MigrationsReader(scriptsPath);

        // Asynchronous
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Arc)
            .SpinnerStyle(Style.Parse("green"))
            .StartAsync("Started cassandra migrations...", async progressContext =>
            {
                progressContext.Status($"Connecting to host - {host} | Keyspace: {keyspace}...");

                var cluster = Cluster.Builder().AddContactPoint($"{host}").Build();
                var session = await cluster.ConnectAsync(keyspace);
                
                progressContext.Status("Connected!");
                var mapper = new Mapper(session);

                progressContext.Status($"Reading migrations from directory:{scriptsPath} ...");

                var fileSystemMigrations = migrationsReader.ReadMigrations();

                var appliedMigrations =
                    (await ReadMigrationsTable(progressContext, session, mapper, keyspace))
                    .ToDictionary(m => m.Version);

                var pendingMigrations = new List<Migration>(); 

                var table = new Table().LeftAligned();

                table.AddColumn("Version");
                table.AddColumn("Name");
                table.AddColumn("Status");

                await foreach (var migration in fileSystemMigrations)
                {
                    if (!appliedMigrations.ContainsKey(migration.Version))
                    {
                        table.AddRow($"[blue]{migration.Version}[/]", migration.Name,
                            Emoji.Known.YellowCircle);
                        pendingMigrations.Add(migration);
                    }
                    else
                    {
                        table.AddRow($"[blue]{migration.Version}[/]", migration.Name,
                            $"[green]{Emoji.Known.CheckMark}[/]");
                    }
                }
                AnsiConsole.Write(table);
                
                progressContext.Status("Applying pending migrations ...");

                foreach (var migration in pendingMigrations)
                {
                    AnsiConsole.WriteLine($"Applying {migration.Version}-{migration.Name}");
                    await mapper.ExecuteAsync(migration.Cql);

                    await mapper.ExecuteAsync(
                        "INSERT INTO schema_migrations (version, cql, name, time) VALUES (?, ?, ?, toTimestamp(now()));",
                        migration.Version, migration.Cql, migration.Name);
                }
                
                AnsiConsole.WriteLine("Migrations completed!");
            });
    }

    private static async Task<IEnumerable<Migration>> ReadMigrationsTable(StatusContext progressContext, ISession session, Mapper mapper, string keyspace)
    {
        progressContext.Status("Reading migrations table...");

        var keyspaceMetadata = session.Cluster.Metadata.GetKeyspace(keyspace);
        var tableNames = keyspaceMetadata.GetTablesNames();
        if (!tableNames.Contains(Constants.MigrationsTableName))
        {
            AnsiConsole.Write(
                $"There is not migration table in keyspace, creating: {Constants.MigrationsTableName}");
            await mapper.ExecuteAsync(
                $"CREATE TABLE {Constants.MigrationsTableName} (version text PRIMARY KEY, name text, cql text, time timestamp)");
            
            return Enumerable.Empty<Migration>(); 
        }

        progressContext.Status("Reading applied migrations...");

        return await mapper.FetchAsync<Migration>($"SELECT * FROM {Constants.MigrationsTableName}");
    }
}