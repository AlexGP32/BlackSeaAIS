public interface IGeoValidationService
{
    bool IsOnLand(double lat, double lon);
    bool IsInSafeZone(double lat, double lon);
}