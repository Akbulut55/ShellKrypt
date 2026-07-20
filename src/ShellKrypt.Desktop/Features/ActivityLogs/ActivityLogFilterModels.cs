using CommunityToolkit.Mvvm.ComponentModel;
using ShellKrypt.Application.Localization;

namespace ShellKrypt.Desktop.Features.ActivityLogs;

public sealed class ActivityFilterOptionVm(string id, string localizationKey, LocalizationService localization) : ObservableObject
{
    public string Id { get; } = id;
    public string Label => localization.Get(localizationKey);
    public void RefreshLocalization() => OnPropertyChanged(nameof(Label));
}

public sealed record ActivityAppliedFilters(
    string Category,
    string Severity,
    string DateRange,
    string Sort,
    bool SearchApplied);
