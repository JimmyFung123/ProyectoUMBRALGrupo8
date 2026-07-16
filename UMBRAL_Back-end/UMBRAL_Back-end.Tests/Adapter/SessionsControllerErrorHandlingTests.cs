namespace UMBRAL_Back_end.Tests.Adapter;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using SessionService.Adapter.Controllers;
using UMBRAL_Back_end.Tests.TestDoubles;
using Xunit;

/// <summary>
/// Cubre el manejo de excepciones de cada acción de <see cref="SessionsController"/> — los
/// bloques catch que las pruebas de integración (camino feliz / NotFound / BadRequest) no
/// ejercitan. Se instancia el controller real con un <see cref="ThrowingSender"/>: una
/// excepción inesperada debe dar 500 y una <see cref="OperationCanceledException"/> debe
/// re-lanzarse (para que el runtime la trate como cancelación, no como error 500).
/// </summary>
public class SessionsControllerErrorHandlingTests
{
    private static readonly Guid Id = Guid.NewGuid();

    private static SessionsController NewController(Exception ex) =>
        new(new ThrowingSender(ex), NullLogger<SessionsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

    private static Task<IActionResult> Invoke(SessionsController c, string action) => action switch
    {
        "GetAll" => c.GetAll(null, null, CancellationToken.None),
        "GetByCode" => c.GetByCode("ABC123", CancellationToken.None),
        "GetById" => c.GetById(Id, CancellationToken.None),
        "GetDashboard" => c.GetDashboard(Id, CancellationToken.None),
        "GetRanking" => c.GetRanking(Id, CancellationToken.None),
        "GetAudit" => c.GetAudit(Id, CancellationToken.None),
        "GetCommandAudit" => c.GetCommandAudit(Id, CancellationToken.None),
        "Cancel" => c.Cancel(Id, CancellationToken.None),
        "Update" => c.Update(Id, new UpdateSessionRequest("Nombre", null), CancellationToken.None),
        "Create" => c.Create(new CreateSessionRequest(Id, "Nombre", null), CancellationToken.None),
        "Start" => c.Start(Id, CancellationToken.None),
        "Pause" => c.Pause(Id, CancellationToken.None),
        "Resume" => c.Resume(Id, CancellationToken.None),
        "Finalize" => c.Finalize(Id, CancellationToken.None),
        "BroadcastOperatorMessage" => c.BroadcastOperatorMessage(Id, new BroadcastOperatorMessageRequest("hola"), CancellationToken.None),
        "ReleaseClue" => c.ReleaseClue(Id, Id, new ReleaseClueRequest(3), CancellationToken.None),
        "PenalizeTeam" => c.PenalizeTeam(Id, Id, new PenalizeTeamRequest(10, "motivo"), CancellationToken.None),
        "ForceAdvanceTeam" => c.ForceAdvanceTeam(Id, Id, CancellationToken.None),
        "GetParticipantStage" => c.GetParticipantStage(Id, Id, CancellationToken.None),
        "SubmitTriviaAnswer" => c.SubmitTriviaAnswer(Id, Id, new SubmitTriviaAnswerRequest(Id, Id, "Equipo"), CancellationToken.None),
        "GetReleasedClues" => c.GetReleasedClues(Id, Id, CancellationToken.None),
        "ValidateQr" => c.ValidateQr(Id, Id, new ValidateQrRequest(Id, "codigo"), CancellationToken.None),
        "LeaveTeam" => c.LeaveTeam(Id, Id, CancellationToken.None),
        _ => throw new ArgumentOutOfRangeException(nameof(action)),
    };

    public static IEnumerable<object[]> Actions() => new[]
    {
        "GetAll", "GetByCode", "GetById", "GetDashboard", "GetRanking", "GetAudit", "GetCommandAudit",
        "Cancel", "Update", "Create", "Start", "Pause", "Resume", "Finalize", "BroadcastOperatorMessage",
        "ReleaseClue", "PenalizeTeam", "ForceAdvanceTeam", "GetParticipantStage", "SubmitTriviaAnswer",
        "GetReleasedClues", "ValidateQr", "LeaveTeam",
    }.Select(a => new object[] { a });

    [Theory]
    [MemberData(nameof(Actions))]
    public async Task Action_Returns500_WhenSenderThrowsUnexpected(string action)
    {
        var result = await Invoke(NewController(new InvalidOperationException("boom")), action);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Theory]
    [MemberData(nameof(Actions))]
    public async Task Action_Rethrows_OnOperationCanceled(string action)
    {
        var act = () => Invoke(NewController(new OperationCanceledException()), action);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task PenalizeTeam_ReturnsBadRequest_OnValidationException()
    {
        var ex = new FluentValidation.ValidationException(new[]
        {
            new FluentValidation.Results.ValidationFailure("Points", "debe ser positivo"),
        });

        var result = await NewController(ex)
            .PenalizeTeam(Id, Id, new PenalizeTeamRequest(-1, "motivo"), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
