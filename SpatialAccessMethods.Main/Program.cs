using ManyConsole;

namespace SpatialAccessMethods.Main;

public class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            args = Console.ReadLine()!.Split(' ');
        }

        var commands = ConsoleCommandDispatcher.FindCommandsInSameAssemblyAs(typeof(Program));
        return ConsoleCommandDispatcher.DispatchCommand(commands, args, Console.Out);
    }
}
