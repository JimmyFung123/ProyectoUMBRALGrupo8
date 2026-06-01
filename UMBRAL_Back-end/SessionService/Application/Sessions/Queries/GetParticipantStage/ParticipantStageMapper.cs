namespace SessionService.Application.Sessions.Queries.GetParticipantStage;

/// <summary>
/// Modela la vista de etapa de un participante (SRP): aísla las reglas de
/// presentación/saneo que antes vivían dentro de <c>ParticipantStageFacade</c>.
///
/// La fachada se queda solo con la orquestación de subsistemas y la decisión de
/// estado (Waiting / Completed / etapa concreta); este mapper decide CÓMO se le
/// muestra esa etapa al participante:
///   • oculta <c>IsCorrect</c> de las opciones (nunca debe salir del servidor),
///   • expone <c>Latitude</c>/<c>Longitude</c> solo en etapas TreasureHunt
///     (el <c>QrCode</c> se queda en el servidor).
///
/// Es una clase pura y sin dependencias: una sola razón de cambio (las reglas de
/// la vista del participante) y se puede probar en aislamiento.
/// </summary>
public static class ParticipantStageMapper
{
    /// <summary>Equipo aún no arrancado (o sesión pendiente): centinela "Waiting".</summary>
    public static ParticipantStageDto Waiting(string sessionStatus) =>
        new(Guid.Empty, "Waiting", "Waiting", 0, null, [], sessionStatus, 0, false);

    /// <summary>Equipo que ya pasó la última etapa: centinela "Completed".</summary>
    public static ParticipantStageDto Completed(string sessionStatus, int currentStageOrder) =>
        new(Guid.Empty, "Completed", "Completed", currentStageOrder, null, [], sessionStatus, currentStageOrder, true);

    /// <summary>
    /// Mapea el detalle real de la etapa al DTO de participante: quita las respuestas
    /// correctas y solo expone coordenadas en TreasureHunt.
    /// </summary>
    public static ParticipantStageDto FromStage(
        StageWithOptionsInfo stage,
        string sessionStatus,
        int currentStageOrder,
        bool isLastStage)
    {
        // Strip IsCorrect — participants only get Id + Text.
        var options = stage.Options
            .Select(o => new ParticipantOptionDto(o.Id, o.Text))
            .ToList();

        // For TreasureHunt stages, expose only Latitude/Longitude (the QrCode stays server-side).
        bool isTreasureHunt = string.Equals(stage.Type, "TreasureHunt", StringComparison.OrdinalIgnoreCase);
        double? latitude = isTreasureHunt ? stage.Latitude : null;
        double? longitude = isTreasureHunt ? stage.Longitude : null;

        return new ParticipantStageDto(
            stage.Id,
            stage.Title,
            stage.Type,
            stage.Order,
            stage.Question,
            options,
            sessionStatus,
            currentStageOrder,
            isLastStage,
            latitude,
            longitude);
    }
}
