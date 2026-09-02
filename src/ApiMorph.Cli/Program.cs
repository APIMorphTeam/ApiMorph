using ApiMorph.Cli.Commands;

const string version = "0.2.0-stage6";

if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
{
    PrintHelp();
    return 0;
}

var command = args[0].ToLowerInvariant();
var commandArgs = args.Skip(1).ToArray();

return command switch
{
    "--version" or "-v" => PrintVersion(),
    "init" => InitCommand.Run(commandArgs),
    "scan" => await ScanCommand.RunAsync(commandArgs),
    "doctor" => await DoctorCommand.RunAsync(commandArgs),
    "status" => await StatusCommand.RunAsync(commandArgs),
    _ => UnknownCommand(command),
};

static int PrintVersion()
{
    Console.WriteLine($"ApiMorph CLI {version}");
    return 0;
}

static int UnknownCommand(string command)
{
    Console.Error.WriteLine($"Unknown command: {command}");
    PrintHelp();
    return 1;
}

static void PrintHelp()
{
    Console.WriteLine("""
    apimorph — ApiMorph installer and operator CLI

    Usage:
      apimorph init
      apimorph scan --owner ORG --repo NAME [--pr]
      apimorph scan --path /examples/stripe-csharp-demo/StripeDemo
      apimorph doctor
      apimorph status

    Commands:
      init            Write deploy/.env interactively
      scan            Run a scan via orchestrator API
      doctor          Check Docker, orchestrator, optional Ollama
      status          Show orchestrator /api/v1/status
      --version, -v   Print CLI version
    """);
}
