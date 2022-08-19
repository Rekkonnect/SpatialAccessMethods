namespace SpatialAccessMethods;

public interface IID
{
    public int ID { get; set; }
    
    public bool IsValid => ID > 0;
}
