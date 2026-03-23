namespace Zone.Models;

public class Activity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public int Day { get; set; }
    public string StartTime { get; set; } = "";
    public string? LocationName { get; set; }
    public int? TerritoryId { get; set; }
    public float? CoordinateX { get; set; }
    public float? CoordinateY { get; set; }
    public float? CoordinateZ { get; set; }
    public string? StreamUrl { get; set; }
}
