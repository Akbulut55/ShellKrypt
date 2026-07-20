using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using ShellKrypt.Application.Activity;
using ShellKrypt.Application.Localization;

namespace ShellKrypt.Desktop.Features.ActivityLogs;

public sealed partial class ActivityLogDetailsViewModel(LocalizationService localization, TimeProvider timeProvider) : ObservableObject
{
    [ObservableProperty] private ActivityItemVm? selectedItem;

    public bool HasSelectedItem => SelectedItem is not null;
    public string SelectedEventId => SelectedItem?.Id ?? localization.Get("Activity.Metadata.NoEvent");
    public string SelectedTimestamp => SelectedItem is null ? localization.Get("Activity.Metadata.NoTimestamp") : FormatTimestamp(SelectedItem.Entry.TimestampUtc);
    public string SelectedCategory => SelectedItem?.CategoryLabel ?? localization.Get("Activity.Category.System");
    public string SelectedStatus => SelectedItem?.SeverityChipText ?? localization.Get("Activity.Severity.Info");
    public string SelectedStatusForeground => SelectedItem?.SeverityForeground ?? "InfoBrush";
    public string SelectedStatusBackground => SelectedItem?.SeverityBackground ?? "InfoMutedBrush";
    public string SelectedAffectedItem => SelectedItem?.AffectedItemDisplay ?? localization.Get("Activity.Metadata.NoItem");
    public string SelectedVault => SelectedItem?.VaultDisplay ?? localization.Get("Activity.Metadata.LocalSession");
    public string SelectedDetail => SelectedItem?.Detail ?? localization.Get("Activity.Metadata.SelectEvent");
    public string SelectedContentChecksum => SelectedItem is null ? localization.Get("Settings.Profile.Unavailable") : ActivityContentChecksum.Compute(SelectedItem.Entry);

    partial void OnSelectedItemChanged(ActivityItemVm? value) => Refresh();

    public void RefreshLocalization() => Refresh();

    private void Refresh()
    {
        OnPropertyChanged(nameof(HasSelectedItem));
        OnPropertyChanged(nameof(SelectedEventId));
        OnPropertyChanged(nameof(SelectedTimestamp));
        OnPropertyChanged(nameof(SelectedCategory));
        OnPropertyChanged(nameof(SelectedStatus));
        OnPropertyChanged(nameof(SelectedStatusForeground));
        OnPropertyChanged(nameof(SelectedStatusBackground));
        OnPropertyChanged(nameof(SelectedAffectedItem));
        OnPropertyChanged(nameof(SelectedVault));
        OnPropertyChanged(nameof(SelectedDetail));
        OnPropertyChanged(nameof(SelectedContentChecksum));
    }

    private string FormatTimestamp(string timestampUtc)
        => DateTimeOffset.TryParse(timestampUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? TimeZoneInfo.ConvertTime(parsed, timeProvider.LocalTimeZone).ToString("MMM d, yyyy | HH:mm:ss", CultureInfo.InvariantCulture)
            : localization.Get("Activity.Time.Unknown");
}
