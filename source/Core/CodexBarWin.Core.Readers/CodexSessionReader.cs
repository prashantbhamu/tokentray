using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using CodexBarWin.Core.Providers;

namespace CodexBarWin.Core.Readers;

public sealed class CodexSessionReader
{
    private readonly string _sessionsDir;

    public CodexSessionReader(string sessionsDir)
    {
        _sessionsDir = sessionsDir;
    }

    public CodexSessionUsage? ReadLatestUsage()
    {
        if (!Directory.Exists(_sessionsDir))
        {
            return null;
        }
        IEnumerable<string> enumerable;
        try
        {
            // FileInfo from enumeration carries its write time, so sorting needs no extra stat per file.
            enumerable = new DirectoryInfo(_sessionsDir).EnumerateFiles("rollout-*.jsonl", SearchOption.AllDirectories)
                .OrderByDescending(f => f.LastWriteTimeUtc).Take(25).Select(f => f.FullName).ToArray();
        }
        catch
        {
            return null;
        }
        foreach (string item in enumerable)
        {
            try
            {
                // The newest usage event is near the end, so read backwards and stop at the first one.
                foreach (string item2 in LinesFromEnd(item))
                {
                    if (item2.Contains("\"token_count\"", StringComparison.Ordinal) && Parse(item2) is { } usage)
                    {
                        return usage;
                    }
                }
            }
            catch
            {
            }
        }
        return null;
    }

    private const int ChunkSize = 64 * 1024;

    internal static IEnumerable<string> LinesFromEnd(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        // Pieces of the line in progress, nearest the end of the file first. Each byte is read, scanned
        // and copied once, so a record spanning many chunks costs linear time, not quadratic.
        var tail = new List<ArraySegment<byte>>();
        long position = stream.Length;
        while (position > 0)
        {
            int count = (int)Math.Min(ChunkSize, position);
            position -= count;
            var chunk = new byte[count];
            stream.Position = position;
            stream.ReadExactly(chunk, 0, count);
            int end = count;
            // '\n' never occurs inside a multi-byte UTF-8 sequence, so splitting on the byte is safe.
            for (int i = count - 1; i >= 0; i--)
            {
                if (chunk[i] != (byte)'\n') continue;
                if (Join(new ArraySegment<byte>(chunk, i + 1, end - i - 1), tail) is { } line) yield return line;
                tail.Clear();
                end = i;
            }
            if (end > 0) tail.Add(new ArraySegment<byte>(chunk, 0, end));
        }
        if (Join(ArraySegment<byte>.Empty, tail) is { } first) yield return first;
    }

    private static string? Join(ArraySegment<byte> head, List<ArraySegment<byte>> tail)
    {
        int length = head.Count;
        foreach (var piece in tail) length += piece.Count;
        if (length == 0) return null;
        var bytes = new byte[length];
        head.CopyTo(bytes, 0);
        int offset = head.Count;
        for (int i = tail.Count - 1; i >= 0; i--)
        {
            tail[i].CopyTo(bytes, offset);
            offset += tail[i].Count;
        }
        return System.Text.Encoding.UTF8.GetString(bytes).TrimEnd('\r');
    }

    internal static CodexSessionUsage? Parse(string? jsonLine)
    {
        if (string.IsNullOrWhiteSpace(jsonLine))
        {
            return null;
        }
        try
        {
            using JsonDocument jsonDocument = JsonDocument.Parse(jsonLine);
            JsonElement rootElement = jsonDocument.RootElement;
            if (!rootElement.TryGetProperty("type", out var value) || value.GetString() != "event_msg")
            {
                return null;
            }
            if (!rootElement.TryGetProperty("payload", out var value2) || value2.ValueKind != JsonValueKind.Object)
            {
                return null;
            }
            if (!value2.TryGetProperty("type", out var value3) || value3.GetString() != "token_count")
            {
                return null;
            }
            if (!value2.TryGetProperty("rate_limits", out var value4) || value4.ValueKind != JsonValueKind.Object)
            {
                return null;
            }
            JsonElement value5;
            string planType = ((value4.TryGetProperty("plan_type", out value5) && value5.ValueKind == JsonValueKind.String) ? value5.GetString() : null);
            CodexWindow codexWindow = Window(value4, "primary");
            CodexWindow codexWindow2 = Window(value4, "secondary");
            DateTimeOffset? sourceUpdatedAt = null;
            if (rootElement.TryGetProperty("timestamp", out var stamp) && stamp.ValueKind == JsonValueKind.String &&
                DateTimeOffset.TryParse(stamp.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
                sourceUpdatedAt = parsed;
            return ((object)codexWindow == null && (object)codexWindow2 == null) ? null : new CodexSessionUsage(planType, codexWindow, codexWindow2, sourceUpdatedAt);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static CodexWindow? Window(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        if (!value.TryGetProperty("used_percent", out var value2) || !value2.TryGetDouble(out var value3))
        {
            return null;
        }
        JsonElement value4;
        int value5;
        int windowMinutes = ((value.TryGetProperty("window_minutes", out value4) && value4.TryGetInt32(out value5)) ? value5 : 0);
        JsonElement value6;
        long value7;
        long num = ((value.TryGetProperty("resets_at", out value6) && value6.TryGetInt64(out value7)) ? value7 : 0);
        if (num <= 0)
        {
            return null;
        }
        return new CodexWindow(value3, windowMinutes, 0L, num);
    }
}
