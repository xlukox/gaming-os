namespace GamingOS.Core;

/// <summary>
/// A game as the UI sees it. Mirrors GameDto in src/ui/src/contracts/api.ts; change both together.
/// </summary>
/// <param name="Platform">"steam", "epic", "gog" or "manual".</param>
/// <param name="PlaytimeMinutes">Null when the platform does not provide it and nothing was tracked yet.</param>
/// <param name="AccentColor">CSS colour the interface takes on while the game is selected.</param>
public sealed record GameDto(long Id, string Title, string Platform, bool Installed, int? PlaytimeMinutes, string AccentColor);
