namespace CodexSwitcher.Core.Abstractions;

/// <summary>Relógio injetável para lógica determinística e testável. Sempre UTC. Ver §8 (datas).</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
