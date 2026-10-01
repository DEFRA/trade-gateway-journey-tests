using Refit;

namespace TradeGateway.Tests;

public static class ApiResponseExtensions
{
    /// <summary>The problem body the gateway sent with a failed response, or null when it succeeded.</summary>
    public static string? ProblemBody(this IApiResponse response) => (response.Error as ApiException)?.Content;
}
