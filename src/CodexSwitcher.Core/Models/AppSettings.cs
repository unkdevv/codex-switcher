namespace CodexSwitcher.Core.Models;

/// <summary>Preferências do app persistidas em settings.json. Ver BUSINESS_RULES.md §2.1/§4.5/§6.</summary>
public sealed class AppSettings
{
    /// <summary>Comportamento de fechar/reabrir apps do Codex no switch.</summary>
    public CloseReopenMode CloseReopenMode { get; set; } = CloseReopenMode.Automatic;

    /// <summary>Sempre exibir o popup de confirmação do switch (padrão). "Não perguntar" é só por sessão.</summary>
    public bool AlwaysConfirmSwitch { get; set; } = true;

    /// <summary>Timeout de fechamento gracioso antes do kill (segundos).</summary>
    public int GracefulCloseTimeoutSeconds { get; set; } = 5;

    /// <summary>Idade (dias) a partir da qual um perfil é elegível a refresh. Ver §6.1.</summary>
    public double RefreshDueDays { get; set; } = 5.0;

    /// <summary>Idade (dias) a partir da qual a UI marca o perfil como Stale (alerta).</summary>
    public double StaleWarningDays { get; set; } = 6.0;

    /// <summary>Janela de obsolescência (dias) após a qual a sessão pode morrer sem refresh.</summary>
    public double StaleHardLimitDays { get; set; } = 8.0;

    /// <summary>Quantos backups do slot ativo manter (rotação). Ver §2.1.</summary>
    public int ActiveSlotBackupsToKeep { get; set; } = 10;

    /// <summary>Caminho manual para o binário codex, caso não esteja no PATH. Nulo = usar PATH.</summary>
    public string? CodexExecutablePathOverride { get; set; }

    /// <summary>Forçar o tema (null = seguir o sistema).</summary>
    public string? ForcedTheme { get; set; }

    public int SchemaVersion { get; set; } = 1;
}
