using System.Reflection;
using System.Text;
using NetCoreApiKit;
using Spectre.Console.Cli;

Console.OutputEncoding = Encoding.UTF8;

var app = new CommandApp<CreateCommand>();
app.Configure(config =>
{
    config.SetApplicationName("netcoreapi-kit");
    config.SetApplicationVersion(Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0");
    config.AddExample("--name", "MiApi", "--security", "--database", "postgresql", "--email", "smtp", "--db-location", "docker", "--yes");
});
return await app.RunAsync(args);
