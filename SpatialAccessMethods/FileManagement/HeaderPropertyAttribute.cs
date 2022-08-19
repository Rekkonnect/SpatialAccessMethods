namespace SpatialAccessMethods.FileManagement;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public class HeaderPropertyAttribute<TPropertyValue, TStoredValue> : Attribute
    where TPropertyValue : unmanaged
    where TStoredValue : unmanaged
{
    public string Name { get; }
    public int CustomOffset { get; }

    public HeaderPropertyAttribute(string name, int customOffset = -1)
    {
        Name = name;
        CustomOffset = customOffset;
    }
}
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class HeaderPropertyAttribute<TPropertyValue> : HeaderPropertyAttribute<TPropertyValue, TPropertyValue>
    where TPropertyValue : unmanaged
{
    public HeaderPropertyAttribute(string name, int customOffset = -1)
        : base(name, customOffset) { }
}
