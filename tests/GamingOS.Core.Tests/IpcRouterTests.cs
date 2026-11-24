using System.Text.Json;
using GamingOS.Core;

namespace GamingOS.Core.Tests;

public class IpcRouterTests
{
    private static JsonElement Parse(string? json) => JsonDocument.Parse(json!).RootElement;

    [Fact]
    public async Task Returns_handler_result_with_the_request_id_and_camelCase_names()
    {
        var router = new IpcRouter();
        router.Register("library.launch", p => Task.FromResult<object?>(new { GameId = p!.Value.GetProperty("gameId").GetInt32() }));

        var response = Parse(await router.HandleAsync("""{"id":7,"method":"library.launch","params":{"gameId":42}}"""));

        Assert.Equal(7, response.GetProperty("id").GetInt32());
        Assert.Equal(42, response.GetProperty("result").GetProperty("gameId").GetInt32());
    }

    [Fact]
    public async Task Request_without_params_passes_null()
    {
        var router = new IpcRouter();
        router.Register("library.list", p => Task.FromResult<object?>(p is null ? Array.Empty<int>() : null));

        var response = Parse(await router.HandleAsync("""{"id":1,"method":"library.list"}"""));

        Assert.Equal(JsonValueKind.Array, response.GetProperty("result").ValueKind);
    }

    [Fact]
    public async Task Unknown_method_returns_an_error_instead_of_throwing()
    {
        var response = Parse(await new IpcRouter().HandleAsync("""{"id":3,"method":"nope"}"""));

        Assert.Equal(3, response.GetProperty("id").GetInt32());
        Assert.Equal("unknown_method", response.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Failing_handler_returns_internal_error()
    {
        var router = new IpcRouter();
        router.Register("boom", _ => throw new InvalidOperationException("Steam not found"));

        var error = Parse(await router.HandleAsync("""{"id":4,"method":"boom"}""")).GetProperty("error");

        Assert.Equal("internal_error", error.GetProperty("code").GetString());
        Assert.Equal("Steam not found", error.GetProperty("message").GetString());
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("""{"method":"library.list"}""")]
    [InlineData("""{"id":"x","method":"library.list"}""")]
    [InlineData("""{"id":1}""")]
    public async Task Malformed_request_is_ignored(string request)
    {
        Assert.Null(await new IpcRouter().HandleAsync(request));
    }
}
