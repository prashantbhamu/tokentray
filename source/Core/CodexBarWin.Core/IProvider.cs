using CodexBarWin.Core.Models;

namespace CodexBarWin.Core;

public interface IProvider
{
    string Id { get; }

    string DisplayName { get; }

    bool IsInstalled();

    UsageSnapshot Refresh();
}
