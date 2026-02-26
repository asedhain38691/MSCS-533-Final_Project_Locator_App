using SQLite;

namespace MauiHeatMap.Models;

public class LocationPoint
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public double Latitude { get; set; }
    public double Longitude { get; set; }

    public DateTime TimestampUtc { get; set; }
}