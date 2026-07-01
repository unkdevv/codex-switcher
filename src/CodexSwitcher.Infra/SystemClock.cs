using CodexSwitcher.Core.Abstractions;

namespace CodexSwitcher.Infra;

/// <summary>Relógio de produção (UTC).</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
