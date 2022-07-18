using Spectre.Console;

namespace Cassandra.Migrations;

public class MigrationsReader
{
    private readonly string _scriptsPath;

    public MigrationsReader(string scriptsPath)
    {
        _scriptsPath = scriptsPath;
    }

    public async IAsyncEnumerable<Migration> ReadMigrations()
    {
        var directory = new DirectoryInfo(_scriptsPath);

        if (!directory.Exists)
        {
            var exception = new DirectoryNotFoundException(_scriptsPath);
            AnsiConsole.WriteException(new DirectoryNotFoundException(_scriptsPath));
            
            throw exception;
        }

        var files = directory.GetFiles("*.cql").OrderBy(f => f.Name);

        foreach (var file in files)
        {
            //TODO Add name validation
            var cql = await File.ReadAllTextAsync(file.FullName);
            yield return new Migration(file.Name, cql);
        }
    }
}