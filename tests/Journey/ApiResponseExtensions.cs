using System.Text.Json;
using Refit;

namespace TradeGateway.Tests;

public static class ApiResponseExtensions
{
    /// <summary>The problem body the gateway sent with a failed response, or null when it succeeded.</summary>
    public static string? ProblemBody(this IApiResponse response) => (response.Error as ApiException)?.Content;

    /// <summary>
    /// Why the gateway refused a reservation: the <c>reason</c> it names in the problem body, such as
    /// <c>QuantitiesInsufficient</c>, or null when there is none.
    /// </summary>
    public static string? ProblemReason(this IApiResponse response)
    {
        if (response.ProblemBody() is not { } body)
            return null;

        using var problem = JsonDocument.Parse(body);
        return problem.RootElement.TryGetProperty("reason", out var reason) ? reason.GetString() : null;
    }
}
