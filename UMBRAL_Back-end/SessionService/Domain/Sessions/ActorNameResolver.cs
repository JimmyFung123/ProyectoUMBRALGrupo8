namespace SessionService.Domain.Sessions;

/// <summary>
/// Domain Service: resuelve el nombre del actor a partir de lo que mandó el
/// operador — vacío/null cae a <see cref="SessionEvent.SystemActor"/>, si no
/// se recorta. El mismo patrón vivía repetido en 3 lugares: dentro de
/// SessionEvent.Create(), en BroadcastOperatorMessageCommandHandler y en
/// PenalizeTeamCommandHandler. SessionEvent.SystemActor sigue siendo la única
/// fuente de verdad de la constante — esta clase la reusa, no la redefine.
/// </summary>
public static class ActorNameResolver
{
    public static string Resolve(string? actorName) =>
        string.IsNullOrWhiteSpace(actorName) ? SessionEvent.SystemActor : actorName.Trim();
}
