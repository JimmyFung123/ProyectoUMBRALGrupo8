namespace ClueService.Adapter.Extensions;

using ClueService.Domain.Common;
using Microsoft.AspNetCore.Mvc;

public static class ResultExtensions
{
    public static IActionResult ToHttpResult<T>(this Result<T> result) =>
        result.IsSuccess
            ? new OkObjectResult(result.Value)
            : MapError(result.Error);

    public static IActionResult ToNoContentResult<T>(this Result<T> result) =>
        result.IsSuccess
            ? new NoContentResult()
            : MapError(result.Error);

    private static IActionResult MapError(Error error) =>
        error.Code.EndsWith("NotFound", StringComparison.OrdinalIgnoreCase)
            ? new NotFoundObjectResult(error)
            : new BadRequestObjectResult(error);
}
