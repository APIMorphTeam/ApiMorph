using System.Diagnostics;
using System.Reflection;

const string version = "0.1.0-stage2";

if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
{
    PrintHelp();
    return 0;
}

var command = args[0].ToLowerInvariant();

return command switch
{
    "--version" or "-v" => PrintVersion(),
    "doctor" => RunDoctor(),
    _ => UnknownCommand(command)
};

static int PrintVersion()
{
    Console.WriteLine($"ApiMorph CLI {version}");
    return 0;
}

static int RunDoctor()
{
    Console.WriteLine("ApiMorph doctor");
    Console.WriteLine("===============");
    Console.WriteLine();

    var allOk = true;

    allOk &= CheckCommand("docker", "--version", "Docker");
    allOk &= CheckCommand("dotnet", "--version", ".NET SDK");

    Console.WriteLine();
    Console.WriteLine(allOk
        ? "All checks passed. You can run: cd deploy && docker compose up --build"
        : "Some checks failed. Install missing prerequisites before continuing.");

    return allOk ? 0 : 1;
}

static bool CheckCommand(string fileName, string arguments, string label)
{
  try
  {
    var psi = new ProcessStartInfo
    {
      FileName = fileName,
      Arguments = arguments,
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      UseShellExecute = false,
      CreateNoWindow = true
    };

    using var process = Process.Start(psi);
    if (process is null)
    {
      Console.WriteLine($"[FAIL] {label}: could not start process");
      return false;
    }

    var output = process.StandardOutput.ReadToEnd().Trim();
    process.WaitForExit();

    if (process.ExitCode == 0)
    {
      Console.WriteLine($"[ OK ] {label}: {output.Split('\n')[0]}");
      return true;
    }

    Console.WriteLine($"[FAIL] {label}: exit code {process.ExitCode}");
    return false;
  }
  catch (Exception ex)
  {
    Console.WriteLine($"[FAIL] {label}: {ex.Message}");
    return false;
  }
}

static int UnknownCommand(string command)
{
  Console.Error.WriteLine($"Unknown command: {command}");
  PrintHelp();
  return 1;
}

static void PrintHelp()
{
  var assembly = Assembly.GetExecutingAssembly().GetName().Name;
  Console.WriteLine($"""
  {assembly} — ApiMorph installer and diagnostics (Stage 2 stub)

  Usage:
    {assembly} --version
    {assembly} doctor

  Commands:
    --version, -v   Print CLI version
    doctor          Check local prerequisites (Docker, .NET SDK)

  Full interactive installer arrives in Stage 6.
  """);
}
