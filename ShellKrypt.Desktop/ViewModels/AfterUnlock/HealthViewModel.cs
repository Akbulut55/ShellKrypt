using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Core.Items;
using ShellKrypt.Infrastructure.Crypto;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class HealthIssueVm : ObservableObject
{
    [ObservableProperty] private string severity = "";
    [ObservableProperty] private string category = "";
    [ObservableProperty] private string title = "";
    [ObservableProperty] private string details = "";
}

public partial class HealthViewModel : ViewModelBase
{
    private const int OldPasswordDays = 90;

    private readonly MainWindowViewModel _root;
    private readonly IItemRepository _repo;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ObservableCollection<HealthIssueVm> Issues { get; } = new();

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string error = "";
    [ObservableProperty] private int analyzedCount;
    [ObservableProperty] private int reusedCount;
    [ObservableProperty] private int weakCount;
    [ObservableProperty] private int oldCount;
    [ObservableProperty] private string lastCheckedText = "Never";

    public HealthViewModel(MainWindowViewModel root, IItemRepository repo)
    {
        _root = root;
        _repo = repo;
        _ = RefreshAsync();
    }

    public string SummaryText => AnalyzedCount == 0
        ? "No web logins were found yet."
        : $"Analyzed {AnalyzedCount} web logins.";
    public string LastCheckedDisplay => $"Last checked: {LastCheckedText}";

    partial void OnAnalyzedCountChanged(int value) => OnPropertyChanged(nameof(SummaryText));
    partial void OnLastCheckedTextChanged(string value) => OnPropertyChanged(nameof(LastCheckedDisplay));

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        Error = "";

        if (_root.VaultPath is null)
        {
            Error = "No vault selected.";
            return;
        }

        IsBusy = true;
        try
        {
            Issues.Clear();
            AnalyzedCount = 0;
            ReusedCount = 0;
            WeakCount = 0;
            OldCount = 0;

            var rows = await _repo.ListAsync(_root.VaultPath);
            var vaultKey = _root.VaultKey;
            var entries = new List<WebLoginHealthItem>();

            foreach (var row in rows.Where(r => r.Header.Type == ItemType.Web))
            {
                var json = AesGcmBlob.Decrypt(vaultKey, row.EncryptedPayload);
                var payload = JsonSerializer.Deserialize<WebPayload>(json, JsonOpts);
                if (payload is null)
                    continue;

                entries.Add(new WebLoginHealthItem(
                    row.Header.Id,
                    payload.Title,
                    payload.Username,
                    payload.Password,
                    ParseUpdated(row.Header.UpdatedAtUtc)));
            }

            AnalyzedCount = entries.Count;
            BuildIssues(entries);
            LastCheckedText = DateTimeOffset.UtcNow.ToString("u");
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void BuildIssues(List<WebLoginHealthItem> entries)
    {
        var reusedGroups = entries
            .Where(x => !string.IsNullOrWhiteSpace(x.Password))
            .GroupBy(x => x.Password)
            .Where(g => g.Count() > 1)
            .OrderByDescending(g => g.Count())
            .ToList();

        foreach (var group in reusedGroups)
        {
            ReusedCount += group.Count();
            Issues.Add(new HealthIssueVm
            {
                Severity = "High",
                Category = "Reused",
                Title = $"{group.Count()} entries share one password",
                Details = string.Join(", ", group.Select(x => x.Title).Where(t => !string.IsNullOrWhiteSpace(t)).Take(5))
            });
        }

        foreach (var item in entries)
        {
            var weaknesses = DescribeWeaknesses(item.Password);
            if (!string.IsNullOrWhiteSpace(weaknesses))
            {
                WeakCount++;
                Issues.Add(new HealthIssueVm
                {
                    Severity = "High",
                    Category = "Weak",
                    Title = item.Title,
                    Details = weaknesses + FormatIdentity(item)
                });
            }

            var age = DateTimeOffset.UtcNow - item.UpdatedAtUtc;
            if (age.TotalDays >= OldPasswordDays)
            {
                OldCount++;
                Issues.Add(new HealthIssueVm
                {
                    Severity = "Medium",
                    Category = "Old",
                    Title = item.Title,
                    Details = $"Last updated {FormatAge(age)} ago" + FormatIdentity(item)
                });
            }
        }
    }

    private static DateTimeOffset ParseUpdated(string value)
    {
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            return parsed;

        return DateTimeOffset.UtcNow;
    }

    private static string DescribeWeaknesses(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return "Empty password.";

        var issues = new List<string>();
        if (password.Length < 12)
            issues.Add($"length {password.Length}");
        if (!password.Any(char.IsLower))
            issues.Add("missing lowercase");
        if (!password.Any(char.IsUpper))
            issues.Add("missing uppercase");
        if (!password.Any(char.IsDigit))
            issues.Add("missing digit");
        if (!password.Any(ch => !char.IsLetterOrDigit(ch)))
            issues.Add("missing symbol");

        return issues.Count == 0
            ? ""
            : "Weak: " + string.Join(", ", issues) + ".";
    }

    private static string FormatIdentity(WebLoginHealthItem item)
    {
        var pieces = new List<string>();
        if (!string.IsNullOrWhiteSpace(item.Username))
            pieces.Add($"User: {item.Username}");

        if (pieces.Count == 0)
            return "";

        return " | " + string.Join(" | ", pieces);
    }

    private static string FormatAge(TimeSpan age)
    {
        if (age.TotalDays >= 365)
            return $"{Math.Floor(age.TotalDays / 365)} year(s)";

        if (age.TotalDays >= 30)
            return $"{Math.Floor(age.TotalDays / 30)} month(s)";

        return $"{Math.Max(1, Math.Floor(age.TotalDays))} day(s)";
    }

    private sealed record WebLoginHealthItem(
        string Id,
        string Title,
        string Username,
        string Password,
        DateTimeOffset UpdatedAtUtc);
}
