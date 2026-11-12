namespace TaxNavi.Application.Services;

/// <summary>
/// Topics TaxNavi refuses to answer with any automated system — grounded rules or LLM —
/// because they need case-specific professional judgement. Checked before the model runs.
/// </summary>
public static class EscalationPolicy
{
    private static readonly (string[] Keywords, string Topic)[] Topics =
    {
        (new[] { "penalt", "fine", "late payment", "interest on late" }, "Penalties"),
        (new[] { "dispute", "objection", "appeal", "tribunal", "audit", "investigat" }, "Disputes and audits"),
        (new[] { "crypto", "bitcoin", "digital asset" }, "Digital assets"),
        (new[] { "capital gain", "sell my house", "sold shares", "sale of land" }, "Capital gains"),
        (new[] { "non-resident", "nonresident", "abroad", "foreign income", "diaspora", "double tax" }, "Residency and foreign income"),
        (new[] { "transfer pricing", "multinational", "group restructur" }, "Group and international tax"),
        (new[] { "evade", "evasion", "hide income", "avoid paying" }, "Compliance")
    };

    /// <summary>Returns the escalation topic for a question, or null when it may be answered.</summary>
    public static string? Match(string question)
    {
        var q = question.Trim().ToLowerInvariant();
        foreach (var (keywords, topic) in Topics)
        {
            if (keywords.Any(q.Contains))
            {
                return topic;
            }
        }

        return null;
    }

    public static string Message(string topic) =>
        $"{topic} depend on facts TaxNavi cannot assess from a calculator. Please speak to a chartered tax practitioner " +
        "(CITN member) or contact the Nigeria Revenue Service or your State Internal Revenue Service directly. " +
        "TaxNavi will not guess on this topic.";
}
