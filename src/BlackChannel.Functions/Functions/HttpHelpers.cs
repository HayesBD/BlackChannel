using System.Net;
using BlackChannel.Shared;
using Microsoft.Azure.Functions.Worker.Http;

namespace BlackChannel.Functions.Functions;

internal static class HttpHelpers
{
    public static async Task<HttpResponseData> Json<T>(this HttpRequestData req, T body, HttpStatusCode status = HttpStatusCode.OK)
    {
        var res = req.CreateResponse(status);
        await res.WriteAsJsonAsync(body, status);
        return res;
    }

    public static async Task<HttpResponseData> Error(this HttpRequestData req, HttpStatusCode status, string message)
    {
        var res = req.CreateResponse(status);
        await res.WriteAsJsonAsync(new ApiError(message), status);
        return res;
    }

    public static Task<HttpResponseData> Unauthorized(this HttpRequestData req)
        => req.Error(HttpStatusCode.Unauthorized, "Not signed in.");
}
