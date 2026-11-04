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
        var buffer = new byte[ChunkSize];
        byte[] carry = Array.Empty<byte>();
        long position = stream.Length;
        while (position > 0)
        {
            int count = (int)Math.Min(ChunkSize, position);
            position -= count;
            stream.Position = position;
            stream.ReadExactly(buffer, 0, count);
            var block = new byte[count + carry.Length];
            Buffer.BlockCopy(buffer, 0, block, 0, count);
            Buffer.BlockCopy(carry, 0, block, count, carry.Length);
            int end = block.Length;
            // '\n' never occurs inside a multi-byte UTF-8 sequence, so splitting on the byte is safe.
            for (int i = block.Length - 1; i >= 0; i--)
            {
                if (block[i] != (byte)'\n') continue;
                if (end - i - 1 > 0) yield return Line(block, i + 1, end);
                end = i;
            }
            carry = block.AsSpan(0, end).ToArray();
        }
        if (carry.Length > 0) yield return Line(carry, 0, carry.Length);
    }

    private static string Line(byte[] bytes, int start, int end) => System.Text.Encoding.UTF8.GetString(bytes, start, end - start).TrimEnd('\r');

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
