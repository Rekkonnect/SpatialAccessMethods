using SpatialAccessMethods.FileManagement;

namespace SpatialAccessMethods.Main;

public sealed class DatabaseFilesCommand : FailableConsoleCommand
{
    public string TableFileName { get; set; }
    public string IndexFileName { get; set; }

    public ActionMode Mode { get; set; }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public DatabaseFilesCommand()
    {
        IsCommand("dbfiles", "View and adjust your current database file names");

        HasLongDescription(
"""
Sets the currently database file names to the specified values.
Both names must be set per invocation of this command.

Usage example: `dbfiles -t "table" -i "index"`
""");

        HasOption("t|table=", "The name of the table file", t => TableFileName = t);
        HasOption("i|index=", "The name of the index (R*-tree) file", i => IndexFileName = i);
        HasOption("v|view", "Views the currently specified file names", _ => Mode = ActionMode.View);
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    protected override int RunUnsafe(string[] remainingArguments)
    {
        GetActionForCurrentMode()();
        return Success;
    }
    private Action GetActionForCurrentMode()
    {
        return Mode switch
        {
            ActionMode.Set => SetFileNames,
            ActionMode.View => ViewFileNames,
        };
    }
    private void SetFileNames()
    {
        DatabaseController.Instance.SetFileNames(TableFileName, IndexFileName);
    }
    private void ViewFileNames()
    {
        Console.WriteLine($"""
                           The currently linked files for the database are:
                                       Table File: {DatabaseController.Instance.TableFileName}
                                       Index File: {DatabaseController.Instance.IndexFileName}
                           Table ID Gap Heap File: {DatabaseController.Instance.TableIDGapFileName}
                           Index ID Gap Heap File: {DatabaseController.Instance.IndexIDGapFileName}
                           """);
    }

    public enum ActionMode
    {
        Set,
        View,
    }
}
