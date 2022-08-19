using ManyConsole;

namespace SpatialAccessMethods.Main;

public sealed class ExitCommand : ConsoleCommand
{
    public const int Value = -80085;

    public ExitCommand()
    {
        IsCommand("exit", "Exits the program");
    }

    public override int Run(string[] remainingArguments)
    {
        return Value;
    }
}
