using System;

namespace CodexBarWin.Core.Providers;

public sealed class ClaudeFetchException : Exception
{
    public ClaudeFetchErrorKind Kind { get; }

    public ClaudeFetchException(ClaudeFetchErrorKind kind, string message)
        : base(message)
    {
        Kind = kind;
    }
}
