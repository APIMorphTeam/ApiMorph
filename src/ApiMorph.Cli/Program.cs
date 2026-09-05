using ApiMorph.Cli.Commands;

const string version = "0.3.0-stage8";

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
    "config" => HandleConfig(commandArgs),
    "repos" => ReposCommand.Run(commandArgs),
    _ => UnknownCommand(command),
};

static int HandleConfig(string[] commandArgs)
{
    if (commandArgs.Length == 0 || commandArgs[0] is "validate")
    {
        return ConfigValidateCommand.Run(commandArgs.Skip(commandArgs.Length > 0 ? 1 : 0).ToArray());
    }

    Console.Error.WriteLine($"Unknown config command: {commandArgs[0]}");
    Console.WriteLine("Usage: apimorph config validate");
    return 1;
}

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
      apimorph doctor
      apimorph status
      apimorph config validate
      apimorph repos add --owner ORG --repo NAME
      apimorph repos list

    Commands:
      init              Write deploy/.env interactively
      scan              Run a scan via orchestrator API (manual / emergency)
      doctor            Check Docker, orchestrator, optional Ollama
      status            Show orchestrator /api/v1/status
      config validate   Parse deploy/config/*.conf
      repos             Manage repos.d registration files
      --version, -v     Print CLI version
    """);
}
