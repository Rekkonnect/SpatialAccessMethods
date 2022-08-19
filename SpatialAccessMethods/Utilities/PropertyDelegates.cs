namespace SpatialAccessMethods.FileManagement;

public record class PropertyDelegates<T>(Func<T> Getter, Action<T> Setter);
