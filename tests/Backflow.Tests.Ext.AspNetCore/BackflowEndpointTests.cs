using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shiron.Backflow.Ext.AspNetCore;
using Xunit;

namespace Shiron.Backflow.Tests.Ext.AspNetCore;

// HTTP integration is necessary for routing, authorization middleware and streaming body limits;
// graph semantics are covered independently by unit tests.
public sealed class BackflowEndpointTests {
    [Fact]
    public async Task MapBackflow_Defaults_RequiresAuthorizationAndHasNoExecutionRoute() {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/graph/catalog")).StatusCode);
        client.DefaultRequestHeaders.Add("X-Test-Identity", "present");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/graph/catalog")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/graph/executions", new { })).StatusCode);
    }

    [Fact]
    public async Task MapBackflow_NamedPolicy_RejectsUnauthorizedIdentity() {
        await using var app = await StartAsync(options => options.AuthorizationPolicy = "restricted");
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Identity", "present");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/graph/catalog")).StatusCode);
    }

    [Fact]
    public async Task MapBackflow_AnonymousOverride_ValidatesAndBuildsWithoutPersisting() {
        await using var app = await StartAsync(options => options.AllowAnonymous = true);
        using var client = app.GetTestClient();
        var graph = new BackflowGraph([], []);
        var validation = await client.PostAsJsonAsync("/api/graph/validate", graph);
        Assert.Equal(HttpStatusCode.OK, validation.StatusCode);
        Assert.True((await validation.Content.ReadFromJsonAsync<BackflowValidationResult>())!.IsValid);
        var creation = await client.PostAsJsonAsync("/api/graph/graphs", graph);
        Assert.Equal(HttpStatusCode.OK, creation.StatusCode);
        Assert.Empty((await creation.Content.ReadFromJsonAsync<BackflowGraphResult>())!.Graph.Nodes);
        var invalid = await client.PostAsJsonAsync("/api/graph/validate", new BackflowGraph([new("bad", "not-published")], []));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(BackflowErrorCode.UnknownNode, Assert.Single((await invalid.Content.ReadFromJsonAsync<BackflowValidationResult>())!.Errors).Code);
    }

    [Fact]
    public async Task MapBackflow_BodyBounds_RejectsKnownAndUnknownLengthsAndBadJson() {
        await using var app = await StartAsync(options => {
            options.AllowAnonymous = true;
            options.MaximumRequestBytes = 64;
            options.MaximumJsonDepth = 4;
        });
        using var client = app.GetTestClient();
        var oversize = new string(' ', 100) + "{}";
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge,
            (await client.PostAsync("/api/graph/validate", new StringContent(oversize, Encoding.UTF8, "application/json"))).StatusCode);
        using var streaming = new UnknownLengthContent(oversize);
        streaming.Headers.ContentType = new("application/json");
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await client.PostAsync("/api/graph/validate", streaming)).StatusCode);
        foreach (var json in new[] { "null", "{", "{\"nodes\":null,\"edges\":[]}", "{\"nodes\":[],\"edges\":[],\"x\":[[[[[[]]]]]]}" }) {
            Assert.Equal(HttpStatusCode.BadRequest,
                (await client.PostAsync("/api/graph/validate", new StringContent(json, Encoding.UTF8, "application/json"))).StatusCode);
        }
        Assert.Equal(HttpStatusCode.UnsupportedMediaType,
            (await client.PostAsync("/api/graph/validate", new StringContent("{}"))).StatusCode);
    }

    [Fact]
    public async Task MapBackflow_OptInExecution_UsesOnlyHostScheduler() {
        var scheduler = new RecordingScheduler();
        await using var app = await StartAsync(options => {
            options.AllowAnonymous = true;
            options.EnableExecution = true;
        }, scheduler);
        using var client = app.GetTestClient();
        var response = await client.PostAsJsonAsync("/api/graph/executions", new BackflowExecutionSubmission(new([], []), []));
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal("host-job", (await response.Content.ReadFromJsonAsync<BackflowExecutionAccepted>())!.JobId);
        Assert.NotNull(scheduler.Request);
        var invalid = await client.PostAsJsonAsync("/api/graph/executions", new BackflowExecutionSubmission(new([], []), null!));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(1, scheduler.Calls);
    }

    [Fact]
    public async Task MapBackflow_DeclarationOnlyCatalog_CreatesButCannotSchedule() {
        var scheduler = new RecordingScheduler();
        await using var app = await StartAsync(options => {
            options.AllowAnonymous = true;
            options.EnableExecution = true;
        }, scheduler, catalog => catalog.AddContract<Configuration>("declaration", ports => ports.Output<int>("value")));
        using var client = app.GetTestClient();
        var graph = new BackflowGraph([new("one", "declaration")], []);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/graph/graphs", graph)).StatusCode);
        var response = await client.PostAsJsonAsync("/api/graph/executions", new BackflowExecutionSubmission(graph, []));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(BackflowErrorCode.NotExecutable,
            Assert.Single((await response.Content.ReadFromJsonAsync<BackflowValidationResult>())!.Errors).Code);
        Assert.Equal(0, scheduler.Calls);
    }

    [Fact]
    public async Task MapBackflow_ExecutionWithoutScheduler_FailsAtStartup() {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddBackflow((_, _) => { }, options => options.EnableExecution = true);
        await using var app = builder.Build();
        Assert.Throws<InvalidOperationException>(() => app.MapBackflow());
    }

    private static async Task<WebApplication> StartAsync(Action<BackflowOptions>? configure = null,
        IBackflowExecutionScheduler? scheduler = null, Action<BackflowCatalogBuilder>? configureCatalog = null) {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("test", _ => { });
        builder.Services.AddAuthorization(options => options.AddPolicy("restricted", policy => policy.RequireClaim("permission", "graph")));
        if (scheduler is not null) builder.Services.AddSingleton(scheduler);
        builder.Services.AddBackflow((_, catalog) => configureCatalog?.Invoke(catalog), configure);
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapBackflow("/api/graph");
        await app.StartAsync();
        return app;
    }

    private sealed class RecordingScheduler : IBackflowExecutionScheduler {
        public BackflowExecutionRequest? Request { get; private set; }
        public int Calls { get; private set; }
        public ValueTask<BackflowExecutionAccepted> ScheduleAsync(BackflowExecutionRequest request, CancellationToken cancellationToken) {
            Request = request;
            Calls++;
            return ValueTask.FromResult(new BackflowExecutionAccepted("host-job"));
        }
    }

    private sealed class TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder) {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(
            Request.Headers.ContainsKey("X-Test-Identity")
                ? AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "test")], Scheme.Name)), Scheme.Name))
                : AuthenticateResult.NoResult());
    }

    private sealed class UnknownLengthContent(string body) : HttpContent {
        protected override bool TryComputeLength(out long length) {
            length = 0;
            return false;
        }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            stream.WriteAsync(Encoding.UTF8.GetBytes(body)).AsTask();
    }

    private sealed record Configuration(int Value);
}
