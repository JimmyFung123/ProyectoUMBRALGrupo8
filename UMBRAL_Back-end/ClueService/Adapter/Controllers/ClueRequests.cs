namespace ClueService.Adapter.Controllers;

public record AddClueRequest(
    Guid StageId,
    int? Order = null,
    string? Content = null,
    double? Latitude = null,
    double? Longitude = null,
    int? RadiusMeters = null,
    int? AutoReleaseAfterMinutes = null);

public record UpdateClueRequest(
    int Order,
    string? Content = null,
    double? Latitude = null,
    double? Longitude = null,
    int? RadiusMeters = null);
