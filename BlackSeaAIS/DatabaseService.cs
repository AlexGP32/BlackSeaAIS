using System.Collections.Concurrent;
using Npgsql;

public class DatabaseService
{
    // Thread-safe connection pool for short-lived DB operations.
    private readonly NpgsqlDataSource _dataSource;
    private readonly IGeoValidationService _geoservice;

    // Absolute speed threshold (knots) to detect GPS spoofing jumps.
    private const double MaxSpeedKnots = 50;

    // Maximum time delta for a valid speed calculation. Older gaps are treated as signal loss.
    private static readonly TimeSpan MaxComparisonGap = TimeSpan.FromMinutes(30);

    // Minimum distance threshold to filter out GPS noise for anchored ships.
    private const double MinDistanceNm = 0.1;

    // In-memory cache of the last PLAUSIBLE position per MMSI.
    // Prevents continuous DB lookups and ensures spoofed points don't validate subsequent spoofed points.
    private readonly ConcurrentDictionary<long, (double Lat, double Lon, DateTime Time)> _lastAccepted = new();

    public DatabaseService(NpgsqlDataSource dataSource, IGeoValidationService geoservice)
    {
        _dataSource = dataSource;
        _geoservice = geoservice;
    }

    // Baseline validation: positions on land indicate bad data or spoofing.
    internal bool DeterminePlausibility(PositionReport pr)
    {
        return !_geoservice.IsOnLand(pr.Latitude, pr.Longitude);
    }

    // Hybrid validation: Combines geometric land checks with kinematic (speed/distance) heuristics.
    // Bypasses false-positive land intersections (e.g., narrow canals, Varna lake) if movement is consistent.
    internal bool DeterminePlausibility(MetaData meta, PositionReport pr, DateTime time)
    {
        bool plausible = DeterminePlausibility(pr);

        if (!_lastAccepted.TryGetValue(meta.MMSI, out var last))
            return plausible;

        TimeSpan gap = AsUtc(time) - last.Time;
        if (gap <= TimeSpan.Zero || gap > MaxComparisonGap)
            return plausible; // Out-of-order message or stale reference

        double distanceNm = GeoMath.DistanceNm(last.Lat, last.Lon, pr.Latitude, pr.Longitude);

        if (distanceNm < MinDistanceNm)
            return true; // Noise threshold

        if (GeoMath.SpeedKnots(distanceNm, gap) > MaxSpeedKnots)
            return false; // Impossible speed jump

        return true; // Consistent kinematic movement overrides low-res polygon land hits
    }

    private static DateTime AsUtc(DateTime time) =>
        time.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(time, DateTimeKind.Utc)
            : time.ToUniversalTime();

    // Caches the latest valid position. Ignores out-of-order stale messages.
    internal void RememberAccepted(MetaData meta, PositionReport pr, DateTime time)
    {
        var t = AsUtc(time);
        _lastAccepted.AddOrUpdate(
            meta.MMSI,
            _ => (pr.Latitude, pr.Longitude, t),
            (_, existing) => t > existing.Time ? (pr.Latitude, pr.Longitude, t) : existing);
    }

    public async Task SavePosition(MetaData meta, PositionReport pr, DateTime time)
    {
        bool isPlausible = DeterminePlausibility(meta, pr, time);

        if (isPlausible)
            RememberAccepted(meta, pr, time);

        await UpsertShip(meta, pr, time, isPlausible);
        await InsertHistory(meta, pr, time, isPlausible);
    }

    // Updates live ship state. 
    // Implausible updates only flag the anomaly without mutating the last known good coordinates.
    public async Task UpsertShip(MetaData meta, PositionReport pr, DateTime time, bool isPlausible)
    {
        var updated = AsUtc(time);

        if (isPlausible)
        {
            await using var cmd = _dataSource.CreateCommand(@"
                INSERT INTO ships (mmsi, ship_name, last_latitude, last_longitude, sog, cog, true_heading, navigational_status, last_update, is_plausible)
                VALUES (@mmsi, @name, @lat, @lon, @sog, @cog, @heading, @navstatus, @updated, @is_plausible)
                ON CONFLICT (mmsi)
                DO UPDATE SET
                    ship_name = EXCLUDED.ship_name,
                    last_latitude = EXCLUDED.last_latitude,
                    last_longitude = EXCLUDED.last_longitude,
                    sog = EXCLUDED.sog,
                    cog = EXCLUDED.cog,
                    true_heading = EXCLUDED.true_heading,
                    navigational_status = EXCLUDED.navigational_status,
                    last_update = EXCLUDED.last_update,
                    is_plausible = EXCLUDED.is_plausible;");

            cmd.Parameters.AddWithValue("mmsi", meta.MMSI);
            cmd.Parameters.AddWithValue("name", meta.ShipName?.Trim() ?? "");
            cmd.Parameters.AddWithValue("lat", pr.Latitude);
            cmd.Parameters.AddWithValue("lon", pr.Longitude);
            cmd.Parameters.AddWithValue("sog", pr.Sog);
            cmd.Parameters.AddWithValue("cog", pr.Cog);
            cmd.Parameters.AddWithValue("heading", pr.TrueHeading);
            cmd.Parameters.AddWithValue("navstatus", pr.NavigationalStatus);
            cmd.Parameters.AddWithValue("updated", updated);
            cmd.Parameters.AddWithValue("is_plausible", isPlausible);

            await cmd.ExecuteNonQueryAsync();
        }
        else
        {
            await using var cmd = _dataSource.CreateCommand(
                "UPDATE ships SET is_plausible = @is_plausible, last_update = @updated WHERE mmsi = @mmsi;");
            cmd.Parameters.AddWithValue("mmsi", meta.MMSI);
            cmd.Parameters.AddWithValue("is_plausible", isPlausible);
            cmd.Parameters.AddWithValue("updated", updated);

            await cmd.ExecuteNonQueryAsync();
        }
    }

    // Appends all records to history for offline spoofing analysis.
    public async Task InsertHistory(MetaData meta, PositionReport pr, DateTime time, bool isPlausible)
    {
        await using var cmd = _dataSource.CreateCommand(
            "INSERT INTO position_history (mmsi, latitude, longitude, recorded_time, is_plausible) " +
            "VALUES (@mmsi, @latitude, @longitude, @recorded_time, @is_plausible);");

        cmd.Parameters.AddWithValue("mmsi", meta.MMSI);
        cmd.Parameters.AddWithValue("latitude", pr.Latitude);
        cmd.Parameters.AddWithValue("longitude", pr.Longitude);
        cmd.Parameters.AddWithValue("recorded_time", AsUtc(time));
        cmd.Parameters.AddWithValue("is_plausible", isPlausible);

        await cmd.ExecuteNonQueryAsync();
    }
}