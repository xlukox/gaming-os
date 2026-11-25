namespace GamingOS.Core;

/// <summary>An installed game as other modules need it: which folder its programs run from.</summary>
public sealed record InstalledGame(long Id, string Platform, string InstallPath);
