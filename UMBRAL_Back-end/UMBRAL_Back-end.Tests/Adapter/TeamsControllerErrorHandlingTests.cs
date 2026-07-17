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
using TeamService.Adapter.Controllers;
using UMBRAL_Back_end.Tests.TestDoubles;
using Xunit;

/// <summary>
/// Cubre el manejo de excepciones de cada acción de <see cref="TeamsController"/> (los
/// bloques catch que las pruebas de integración no ejercitan): 500 ante excepción
/// inesperada y rethrow de <see cref="OperationCanceledException"/>. Se instancia el
/// controller real con un <see cref="ThrowingSender"/>.
/// </summary>
public class TeamsControllerErrorHandlingTests
{
    private static readonly Guid Id = Guid.NewGuid();

    private static TeamsController NewController(Exception ex) =>
        new(new ThrowingSender(ex), NullLogger<TeamsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

    private static Task<IActionResult> Invoke(TeamsController c, string action) => action switch
    {
        "Create" => c.Create(new CreateTeamRequest(Id, "Equipo"), CancellationToken.None),
        "Join" => c.Join("CODE12", CancellationToken.None),
        "GetById" => c.GetById(Id, CancellationToken.None),
        "GetTeamProgress" => c.GetTeamProgress(Id, CancellationToken.None),
        "GetSessionRanking" => c.GetSessionRanking(Id, CancellationToken.None),
        "ReleaseClue" => c.ReleaseClue(Id, new ReleaseClueRequest(3), CancellationToken.None),
        "Leave" => c.Leave(Id, CancellationToken.None),
        "Penalize" => c.Penalize(Id, new PenalizeTeamRequest(10, "motivo"), CancellationToken.None),
        "ForceAdvance" => c.ForceAdvance(Id, new ForceAdvanceTeamRequest(2), CancellationToken.None),
        "RecordWrongAttempt" => c.RecordWrongAttempt(Id, new RecordWrongAttemptRequest(Id, 5), CancellationToken.None),
        "RecordEvidenceOutcome" => c.RecordEvidenceOutcome(Id, new RecordEvidenceOutcomeRequest(true, 10, 2), CancellationToken.None),
        _ => throw new ArgumentOutOfRangeException(nameof(action)),
    };

    public static IEnumerable<object[]> Actions() => new[]
    {
        "Create", "Join", "GetById", "GetTeamProgress", "GetSessionRanking", "ReleaseClue",
        "Leave", "Penalize", "ForceAdvance", "RecordWrongAttempt", "RecordEvidenceOutcome",
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
}
