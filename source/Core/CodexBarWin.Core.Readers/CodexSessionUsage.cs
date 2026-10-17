using System;
using CodexBarWin.Core.Providers;

namespace CodexBarWin.Core.Readers;

public sealed record CodexSessionUsage(string? PlanType, CodexWindow? Primary, CodexWindow? Secondary, DateTimeOffset? SourceUpdatedAt = null);
