namespace CodexSwitcher.Core.Support;

/// <summary>
/// Formata datas em tempo relativo humano a partir de valores UTC. Ver BUSINESS_RULES.md §8.
/// Suporta pt e en; a data absoluta (para tooltip) é responsabilidade da UI.
/// </summary>
public static class RelativeTime
{
    public static string Humanize(DateTimeOffset? value, DateTimeOffset now, bool portuguese = true)
    {
        if (value is not { } v)
            return portuguese ? "nunca" : "never";

        var delta = now - v;
        if (delta < TimeSpan.Zero)
            return portuguese ? "agora mesmo" : "just now";

        if (delta.TotalMinutes < 1)
            return portuguese ? "agora mesmo" : "just now";

        if (delta.TotalMinutes < 60)
        {
            var m = (int)delta.TotalMinutes;
            return portuguese ? $"há {m} min" : $"{m} min ago";
        }

        if (delta.TotalHours < 24)
        {
            var h = (int)delta.TotalHours;
            return portuguese
                ? (h == 1 ? "há 1 hora" : $"há {h} horas")
                : (h == 1 ? "1 hour ago" : $"{h} hours ago");
        }

        if (delta.TotalDays < 7)
        {
            var d = (int)delta.TotalDays;
            return portuguese
                ? (d == 1 ? "ontem" : $"há {d} dias")
                : (d == 1 ? "yesterday" : $"{d} days ago");
        }

        if (delta.TotalDays < 30)
        {
            var w = (int)(delta.TotalDays / 7);
            return portuguese
                ? (w == 1 ? "há 1 semana" : $"há {w} semanas")
                : (w == 1 ? "1 week ago" : $"{w} weeks ago");
        }

        return v.ToLocalTime().ToString("dd/MM/yyyy");
    }
}
