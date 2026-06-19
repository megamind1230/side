using System.Collections.Generic;
using NextLearn.Desktop.Models;

namespace NextLearn.Desktop.Services;

public interface IKeyBindingService
{
    string ActiveProfile { get; }

    IReadOnlyList<string> AvailableProfiles { get; }

    IReadOnlyList<KeyBinding> CurrentBindings { get; }

    void SwitchProfile(string name);

    bool TrySaveCustomBindings(IReadOnlyList<KeyBinding> bindings, out string? error);
}
