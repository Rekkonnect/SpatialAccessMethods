using ManyConsole;

namespace SpatialAccessMethods.Main;

public abstract class FailableConsoleCommand : ConsoleCommand
{
    public const int Success = 0;
    public const int Failure = 0xFA17ED;

    protected abstract int RunUnsafe(string[] remainingArguments);

    public sealed override int Run(string[] remainingArguments)
    {
        try
        {
            return RunUnsafe(remainingArguments);
        }
        catch (Exception ex)
        {
            WriteException(ex);
            return Failure;
        }
    }
    private static void WriteException(Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine(ex.Message);
        Console.Error.WriteLine(ex.StackTrace);
        Console.Error.WriteLine();

        if (ex.InnerException is not null)
            WriteException(ex.InnerException);

        Console.ResetColor();
    }
}
