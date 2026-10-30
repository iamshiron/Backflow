using System.Buffers;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Shiron.Backflow.Ext.AspNetCore;

/// <summary>Stateless, bounded HTTP endpoints over the host's Backflow catalog.</summary>
public static class BackflowEndpointRouteBuilderExtensions {
    /// <summary>Map catalog, graph creation and validation; execution admission is explicitly opt-in.</summary>
    /// <remarks>All endpoints require authorization by default. Graph creation builds but does not save a graph.</remarks>
    public static RouteGroupBuilder MapBackflow(this IEndpointRouteBuilder endpoints, string prefix = "/backflow") {
        var options = endpoints.ServiceProvider.GetRequiredService<BackflowOptions>();
        _ = endpoints.ServiceProvider.GetRequiredService<BackflowGraphService>();
        if (options.EnableExecution && !endpoints.ServiceProvider.IsService<IBackflowExecutionScheduler>())
            throw new InvalidOperationException("Execution requires a host-provided IBackflowExecutionScheduler.");
        var group = endpoints.MapGroup(prefix);
        if (options.AllowAnonymous) group.AllowAnonymous();
        else if (options.AuthorizationPolicy is { } policy) group.RequireAuthorization(policy);
        else group.RequireAuthorization();

        group.MapGet("/catalog", (BackflowGraphService graphs) => graphs.GetCatalog())
            .Produces<BackflowCatalogDescriptor>();
        group.MapPost("/validate", async (HttpContext http, BackflowGraphService graphs) => {
            var body = await ReadAsync<BackflowGraph>(http, options);
            if (body.Error is not null) return body.Error;
            var result = graphs.Validate(body.Value!, http.RequestAborted);
            return Results.Json(result, statusCode: result.IsValid ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest);
        }).Accepts<BackflowGraph>("application/json").Produces<BackflowValidationResult>()
            .Produces<BackflowValidationResult>(StatusCodes.Status400BadRequest)
            .Produces<BackflowValidationResult>(StatusCodes.Status413PayloadTooLarge);
        group.MapPost("/graphs", async (HttpContext http, BackflowGraphService graphs) => {
            var body = await ReadAsync<BackflowGraph>(http, options);
            if (body.Error is not null) return body.Error;
            if (!graphs.TryCompile(body.Value!, out _, out var result, http.RequestAborted))
                return Results.BadRequest(result);
            return Results.Ok(new BackflowGraphResult(body.Value!));
        }).Accepts<BackflowGraph>("application/json").Produces<BackflowGraphResult>()
            .Produces<BackflowValidationResult>(StatusCodes.Status400BadRequest)
            .Produces<BackflowValidationResult>(StatusCodes.Status413PayloadTooLarge);

        if (options.EnableExecution) {
            group.MapPost("/executions", async (HttpContext http, BackflowGraphService graphs,
                IBackflowExecutionScheduler scheduler) => {
                var body = await ReadAsync<BackflowExecutionSubmission>(http, options);
                if (body.Error is not null) return body.Error;
                if (!graphs.TryPrepareExecution(body.Value!, out var request, out var result, http.RequestAborted))
                    return Results.BadRequest(result);
                var accepted = await scheduler.ScheduleAsync(request!, http.RequestAborted);
                return Results.Json(accepted, statusCode: StatusCodes.Status202Accepted);
            }).Accepts<BackflowExecutionSubmission>("application/json").Produces<BackflowExecutionAccepted>(StatusCodes.Status202Accepted)
                .Produces<BackflowValidationResult>(StatusCodes.Status400BadRequest)
                .Produces<BackflowValidationResult>(StatusCodes.Status413PayloadTooLarge);
        }
        return group;
    }

    private static bool IsService<T>(this IServiceProvider provider) =>
        provider.GetRequiredService<IServiceProviderIsService>().IsService(typeof(T));

    private static async ValueTask<(T? Value, IResult? Error)> ReadAsync<T>(HttpContext http, BackflowOptions options) {
        if (!http.Request.HasJsonContentType())
            return (default, Error(BackflowErrorCode.InvalidRequest, "Content-Type must be application/json.", StatusCodes.Status415UnsupportedMediaType));
        if (http.Request.ContentLength > options.MaximumRequestBytes)
            return (default, Error(BackflowErrorCode.LimitExceeded, "The request body exceeds the configured byte limit.", StatusCodes.Status413PayloadTooLarge));
        var buffer = ArrayPool<byte>.Shared.Rent(Math.Min(options.MaximumRequestBytes, 8192));
        try {
            using var body = new MemoryStream();
            while (true) {
                var count = await http.Request.Body.ReadAsync(buffer.AsMemory(), http.RequestAborted);
                if (count == 0) break;
                if (body.Length + count > options.MaximumRequestBytes)
                    return (default, Error(BackflowErrorCode.LimitExceeded, "The request body exceeds the configured byte limit.", StatusCodes.Status413PayloadTooLarge));
                await body.WriteAsync(buffer.AsMemory(0, count), http.RequestAborted);
            }
            body.Position = 0;
            var serializer = new JsonSerializerOptions(JsonSerializerDefaults.Web) {
                MaxDepth = options.MaximumJsonDepth,
                RespectRequiredConstructorParameters = true
            };
            try {
                var value = await JsonSerializer.DeserializeAsync<T>(body, serializer, http.RequestAborted);
                return value is null
                    ? (default, Error(BackflowErrorCode.InvalidRequest, "A non-null JSON request is required.", StatusCodes.Status400BadRequest))
                    : (value, null);
            } catch (JsonException) {
                return (default, Error(BackflowErrorCode.InvalidRequest, "The request is not valid JSON for this endpoint.", StatusCodes.Status400BadRequest));
            }
        } finally {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    private static IResult Error(BackflowErrorCode code, string message, int status) =>
        Results.Json(new BackflowValidationResult(false, [new(code, message)]), statusCode: status);
}
