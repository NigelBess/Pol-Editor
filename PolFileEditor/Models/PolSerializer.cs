using System.Text;

namespace PolFileEditor.Models;

/// <summary>
/// Serializes a <see cref="PolDocument"/> back to .pol text: the preserved header, then
/// each task as a "# Task N" section, with a single blank line between task groups and
/// "-" for every unused field. Rule numbers are regenerated as N.M.
/// </summary>
public static class PolSerializer
{
    public static string Serialize(PolDocument doc)
    {
        var sb = new StringBuilder();

        var header = HeaderFormatter.ToComments(doc.HeaderText);
        if (header.Length > 0)
        {
            sb.Append(header);
            sb.Append('\n');
        }

        for (var t = 0; t < doc.Tasks.Count; t++)
        {
            // Blank line separating groups (and separating the header from the first task).
            sb.Append('\n');

            var taskNumber = t + 1;
            sb.Append("# Task ").Append(taskNumber).Append('\n');

            var rules = doc.Tasks[t].Rules;
            for (var r = 0; r < rules.Count; r++)
            {
                sb.Append(SerializeRule($"{taskNumber}.{r}", rules[r])).Append('\n');
            }
        }

        return sb.ToString();
    }

    private static string SerializeRule(string ruleNumber, PolRule rule)
    {
        return string.Join(',',
            ruleNumber,
            string.IsNullOrWhiteSpace(rule.Action) ? "Block" : rule.Action.Trim(),
            Dash(rule.MacSource),
            Dash(rule.MacDest),
            Dash(rule.IpSource),
            Dash(rule.IpDest),
            Dash(rule.IpProtocol),
            Dash(rule.PortSource),
            Dash(rule.PortDest),
            rule.Comment.Trim());
    }

    // Empty field -> "-" (the unused marker); otherwise the trimmed value.
    private static string Dash(string value)
        => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
}
