namespace GamingOS.Core;

/// <summary>
/// One official announcement for a game. Mirrors NewsItemDto in src/ui/src/contracts/api.ts; change both together.
/// </summary>
public sealed record NewsItemDto(string Title, string Url, DateTimeOffset PublishedAt);
