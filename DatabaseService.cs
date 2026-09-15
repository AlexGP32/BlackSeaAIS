using Npgsql;

public class DatabaseService

{
    private readonly NpgsqlConnection _conn;
    private readonly GeoValidationService _geoservice;

    public DatabaseService(NpgsqlConnection conn, GeoValidationService geoservice)
    {
        _conn = conn;
        _geoservice = geoservice;
    }

    public async Task UpsertShip(MetaData meta, PositionReport pr, DateTime time)
    {
        bool isPlausible = !_geoservice.IsOnLand(pr.Latitude, pr.Longitude);
        if (isPlausible)
        {
            var cmd = new NpgsqlCommand(@"INSERT INTO ships (mmsi, ship_name, last_latitude, last_longitude, sog, cog, true_heading, navigational_status, last_update, is_plausible) 
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
    is_plausible = EXCLUDED.is_plausible;",
     _conn);
            cmd.Parameters.AddWithValue("mmsi", meta.MMSI);
            cmd.Parameters.AddWithValue("name", meta.ShipName?.Trim() ?? "");
            cmd.Parameters.AddWithValue("lat", pr.Latitude);
            cmd.Parameters.AddWithValue("lon", pr.Longitude);
            cmd.Parameters.AddWithValue("sog", pr.Sog);
            cmd.Parameters.AddWithValue("cog", pr.Cog);
            cmd.Parameters.AddWithValue("heading", pr.TrueHeading);
            cmd.Parameters.AddWithValue("navstatus", pr.NavigationalStatus);
            cmd.Parameters.AddWithValue("updated", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("is_plausible", isPlausible);
            await cmd.ExecuteNonQueryAsync();
        }
        else
        {
            var cmd = new NpgsqlCommand(@"UPDATE ships SET is_plausible = @is_plausible, last_update = @updated WHERE mmsi = @mmsi;", _conn);
            cmd.Parameters.AddWithValue("mmsi", meta.MMSI);
            cmd.Parameters.AddWithValue("is_plausible", isPlausible);
            cmd.Parameters.AddWithValue("updated", DateTime.UtcNow);
            await cmd.ExecuteNonQueryAsync();
        }
    }
    public async Task InsertHistory(MetaData meta, PositionReport pr, DateTime time)
    {
        bool isPlausible = !_geoservice.IsOnLand(pr.Latitude, pr.Longitude);
        var cmdHistory = new NpgsqlCommand(@"INSERT INTO position_history (mmsi, latitude, longitude, recorded_time, is_plausible) VALUES (@mmsi, @latitude, @longitude, @recorded_time, @is_plausible);", _conn);
        cmdHistory.Parameters.AddWithValue("mmsi", meta.MMSI);
        cmdHistory.Parameters.AddWithValue("latitude", pr.Latitude);
        cmdHistory.Parameters.AddWithValue("longitude", pr.Longitude);
        cmdHistory.Parameters.AddWithValue("recorded_time", time);
        cmdHistory.Parameters.AddWithValue("is_plausible", isPlausible);
        await cmdHistory.ExecuteNonQueryAsync();
    }
}