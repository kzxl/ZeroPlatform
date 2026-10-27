namespace ZeroUI.Core.Gis
{
    public class GeoPoint
    {
        public double Lat { get; set; }
        public double Lon { get; set; }
    }

    public class MapMarker
    {
        public double Lat { get; set; }
        public double Lon { get; set; }
        public string Label { get; set; } = string.Empty;
        public string Glyph { get; set; } = string.Empty;
        public object? Tag { get; set; }
    }

    public class GeoFencePoint
    {
        public double Lat { get; set; }
        public double Lon { get; set; }
    }

    public class RoutePoint
    {
        public double Lat { get; set; }
        public double Lon { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class AssetLocation
    {
        public string AssetId { get; set; } = string.Empty;
        public double Lat { get; set; }
        public double Lon { get; set; }
    }

    public class Route
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class GeoFence
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class AssetPin
    {
        public double X { get; set; }
        public double Y { get; set; }
        public string Label { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}