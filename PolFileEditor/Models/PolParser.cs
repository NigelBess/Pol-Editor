using System.Globalization;

namespace PolFileEditor.Models;

/// <summary>Parses .pol file text into a <see cref="PolDocument"/>.</summary>
public static class PolParser
{
    private const int ExpectedFieldCount = 10;

    public static PolDocument Parse(string text)
    {
        var doc = new PolDocument();
        // Split on any newline style, preserving blank lines. Trim a trailing empty
        // element that a final newline would produce.
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();

        // 1) Header preamble: leading lines up to (not including) the first "# Task N"
        //    marker or the first data row.
        var bodyStart = 0;
        while (bodyStart < lines.Count)
        {
            var line = lines[bodyStart];
            if (IsTaskMarker(line, out _) || IsDataRow(line))
                break;
            bodyStart++;
        }

        var header = string.Join("\n", lines.Take(bodyStart)).TrimEnd('\n');
        doc.HeaderText = HeaderFormatter.ToDisplay(header);

        // 2) Body: task markers create tasks; data rows attach to the task named by the
        //    integer part of their rule number.
        var tasksByNumber = new Dictionary<int, PolTask>();

        PolTask GetOrCreateTask(int number)
        {
            if (!tasksByNumber.TryGetValue(number, out var task))
            {
                task = new PolTask { Number = number };
                tasksByNumber[number] = task;
            }
            return task;
        }

        var currentTaskNumber = 1;
        for (var i = bodyStart; i < lines.Count; i++)
        {
            var line = lines[i];

            if (IsTaskMarker(line, out var markerNumber))
            {
                currentTaskNumber = markerNumber;
                GetOrCreateTask(markerNumber); // preserve empty tasks
                continue;
            }

            if (!IsDataRow(line))
                continue; // ignore stray comments / blank lines in the body

            var rule = ParseRule(line, out var ruleTaskNumber);
            var taskNumber = ruleTaskNumber ?? currentTaskNumber;
            GetOrCreateTask(taskNumber).Rules.Add(rule);
        }

        // 3) Order tasks ascending by their original number.
        foreach (var task in tasksByNumber.Values.OrderBy(t => t.Number))
        {
            doc.Tasks.Add(task);
        }

        return doc;
    }

    private static bool IsTaskMarker(string line, out int number)
    {
        number = 0;
        var trimmed = line.Trim();
        if (!trimmed.StartsWith('#'))
            return false;

        var content = trimmed.TrimStart('#').Trim();
        if (!content.StartsWith("Task", StringComparison.OrdinalIgnoreCase))
            return false;

        var rest = content[4..].Trim();
        // Accept "Task 3", "Task 3:" etc. Take the leading integer if present.
        var digits = new string(rest.TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out number);
    }

    private static bool IsDataRow(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.Length > 0 && !trimmed.StartsWith('#') && line.Contains(',');
    }

    private static PolRule ParseRule(string line, out int? taskNumber)
    {
        // Split into at most 10 fields so commas inside the trailing comment are preserved.
        var fields = line.Split(',', ExpectedFieldCount);
        string Field(int index) => index < fields.Length ? fields[index].Trim() : "";
        string Clean(int index) => Normalize(Field(index));

        taskNumber = ParseTaskNumber(Field(0));

        return new PolRule
        {
            Action = string.IsNullOrWhiteSpace(Field(1)) ? "Block" : Field(1),
            MacSource = Clean(2),
            MacDest = Clean(3),
            IpSource = Clean(4),
            IpDest = Clean(5),
            IpProtocol = Clean(6),
            PortSource = Clean(7),
            PortDest = Clean(8),
            // Do not trim the comment's internal content beyond the surrounding whitespace.
            Comment = fields.Length > 9 ? fields[9].Trim() : ""
        };
    }

    private static int? ParseTaskNumber(string ruleNumber)
    {
        if (string.IsNullOrWhiteSpace(ruleNumber))
            return null;

        var intPart = new string(ruleNumber.Trim().TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(intPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;
    }

    // "-" (the unused marker) becomes an empty string in the model.
    private static string Normalize(string value) => value == "-" ? "" : value;
}
