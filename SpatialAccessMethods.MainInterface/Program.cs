using SpatialAccessMethods.Main;
using System.Diagnostics;
using System.Text;

Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;

Console.WriteLine("Welcome to the world of spatial data");

RunCommand("help");

while (true)
{
    Console.WriteLine("Enter the command of your choice:");
    var command = Console.ReadLine()!;
    try
    {
        int result = RunCommand(command);
        if (result is ExitCommand.Value)
            return;
    }
    catch { }
}

static int RunCommand(string command)
{
    Debug.WriteLine(Environment.CurrentDirectory);
    var processStartInfo = new ProcessStartInfo(@"SpatialAccessMethods.Main.exe", command)
    {
    };

    var process = Process.Start(processStartInfo)!;
    process.WaitForExit();
    return process.ExitCode;
}
