using System.Text.Json.Serialization;

namespace CodexBarWin.Core.Models;

[JsonConverter(typeof(JsonStringEnumConverter<UsageDataSource>))]
public enum UsageDataSource { Unknown, Live, LocalSession, LocalLog, Cached }
