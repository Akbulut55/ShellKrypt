using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Core.Items;
using ShellKrypt.Infrastructure.Crypto;

namespace ShellKrypt.Desktop.ViewModels;

public sealed record AllItemEntry(
    string Id,
    ItemType Type,
    string Title,
    string SecondaryText,
    string Snippet,
    IReadOnlyList<string> Labels,
    string SearchText,
    string CreatedAtUtc,
    string UpdatedAtUtc)
{
    public string TypeLabel => Type.ToString();
    public string IconLetter => string.IsNullOrWhiteSpace(Title) ? "?" : Title.Trim()[0].ToString().ToUpperInvariant();
    public string LabelsDisplay => Labels.Count == 0 ? "No labels" : string.Join(", ", Labels);
}

public sealed partial class AllItemsViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _root;
    private readonly ShellViewModel _shell;
    private readonly IItemRepository _repo;
    private readonly List<AllItemEntry> _allItems = new();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ObservableCollection<AllItemEntry> Rows { get; } = new();
    public ObservableCollection<string> TypeFilters { get; } = new()
    {
        "All",
        "Web",
        "Card",
        "Note"
    };
    public ObservableCollection<string> LabelFilters { get; } = new()
    {
        "All labels"
    };

    [ObservableProperty] private AllItemEntry? selectedRow;
    [ObservableProperty] private string searchText = "";
    [ObservableProperty] private string selectedTypeFilter = "All";
    [ObservableProperty] private string selectedLabelFilter = "All labels";
    [ObservableProperty] private string selectedLabelsText = "";
    [ObservableProperty] private string error = "";
    [ObservableProperty] private bool isBusy;

    [ObservableProperty] private int totalCount;
    [ObservableProperty] private int webCount;
    [ObservableProperty] private int cardCount;
    [ObservableProperty] private int noteCount;
    [ObservableProperty] private int filteredCount;

    public AllItemsViewModel(MainWindowViewModel root, ShellViewModel shell, IItemRepository repo)
    {
        _root = root;
        _shell = shell;
        _repo = repo;

        _ = LoadAsync();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSelectedTypeFilterChanged(string value) => ApplyFilter();
    partial void OnSelectedLabelFilterChanged(string value) => ApplyFilter();

    partial void OnSelectedRowChanged(AllItemEntry? value)
    {
        SelectedLabelsText = value is null
            ? ""
            : string.Join(", ", value.Labels);
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync(SelectedRow?.Id);

    [RelayCommand]
    private async Task SaveLabelsAsync()
    {
        Error = "";

        if (_root.VaultPath is null)
        {
            Error = "No vault selected.";
            return;
        }

        if (SelectedRow is null)
        {
            Error = "Select an item first.";
            return;
        }

        IsBusy = true;
        try
        {
            var names = ParseLabelNames(SelectedLabelsText);
            var labelIds = new List<string>();

            foreach (var name in names)
            {
                var label = await _repo.UpsertLabelAsync(_root.VaultPath, name);
                labelIds.Add(label.Id);
            }

            await _repo.SetItemLabelsAsync(_root.VaultPath, SelectedRow.Id, labelIds);
            await LoadAsync(SelectedRow.Id);
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

    [RelayCommand]
    private void OpenSelected()
    {
        if (SelectedRow is not null)
            OpenRow(SelectedRow);
    }

    [RelayCommand]
    private void OpenRow(AllItemEntry? row)
    {
        if (row is null)
            return;

        Error = "";

        switch (row.Type)
        {
            case ItemType.Web:
                _shell.ShowWebLogins();
                break;
            case ItemType.Card:
                _shell.ShowCards();
                break;
            case ItemType.Note:
                _shell.ShowSecureNotes();
                break;
        }
    }

    private async Task LoadAsync(string? selectItemId = null)
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
            _allItems.Clear();
            Rows.Clear();
            LabelFilters.Clear();
            LabelFilters.Add("All labels");

            var rows = await _repo.ListAsync(_root.VaultPath);
            var labels = await _repo.ListLabelsAsync(_root.VaultPath);

            foreach (var label in labels.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
                LabelFilters.Add(label.Name);

            foreach (var row in rows)
            {
                _allItems.Add(BuildEntry(row));
            }

            TotalCount = _allItems.Count;
            WebCount = _allItems.Count(x => x.Type == ItemType.Web);
            CardCount = _allItems.Count(x => x.Type == ItemType.Card);
            NoteCount = _allItems.Count(x => x.Type == ItemType.Note);

            if (!string.IsNullOrWhiteSpace(SelectedLabelFilter) &&
                SelectedLabelFilter != "All labels" &&
                !LabelFilters.Contains(SelectedLabelFilter))
            {
                SelectedLabelFilter = "All labels";
            }

            ApplyFilter();

            if (!string.IsNullOrWhiteSpace(selectItemId))
            {
                SelectedRow = Rows.FirstOrDefault(x => x.Id == selectItemId);
            }
            else if (SelectedRow is not null)
            {
                SelectedRow = Rows.FirstOrDefault(x => x.Id == SelectedRow.Id);
            }

            FilteredCount = Rows.Count;
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

    private void ApplyFilter()
    {
        Rows.Clear();

        var query = SearchText?.Trim();
        var typeFilter = SelectedTypeFilter;
        var labelFilter = SelectedLabelFilter;

        IEnumerable<AllItemEntry> filtered = _allItems;

        if (!string.IsNullOrWhiteSpace(typeFilter) && typeFilter != "All")
        {
            filtered = filtered.Where(x => x.TypeLabel.Equals(typeFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(labelFilter) && labelFilter != "All labels")
        {
            filtered = filtered.Where(x => x.Labels.Any(label => label.Equals(labelFilter, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = filtered.Where(x => x.SearchText.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var row in filtered)
            Rows.Add(row);

        FilteredCount = Rows.Count;

        if (SelectedRow is not null && !Rows.Any(x => x.Id == SelectedRow.Id))
            SelectedRow = Rows.FirstOrDefault();
    }

    private AllItemEntry BuildEntry(VaultItemRow row)
    {
        var labels = row.Labels.Select(x => x.Name).ToArray();

        return row.Header.Type switch
        {
            ItemType.Web => BuildWebEntry(row, labels),
            ItemType.Card => BuildCardEntry(row, labels),
            ItemType.Note => BuildNoteEntry(row, labels),
            _ => new AllItemEntry(
                row.Header.Id,
                row.Header.Type,
                "Unknown",
                "",
                "",
                labels,
                string.Join(" ", labels),
                row.Header.CreatedAtUtc,
                row.Header.UpdatedAtUtc)
        };
    }

    private AllItemEntry BuildWebEntry(VaultItemRow row, IReadOnlyList<string> labels)
    {
        var json = AesGcmBlob.Decrypt(_root.VaultKey, row.EncryptedPayload);
        var payload = JsonSerializer.Deserialize<WebPayload>(json, JsonOpts)
            ?? new WebPayload("", "", "", "", "", "");

        var secondary = string.IsNullOrWhiteSpace(payload.Username)
            ? payload.Url
            : string.IsNullOrWhiteSpace(payload.Url)
                ? payload.Username
                : $"{payload.Username} / {payload.Url}";

        var snippetSource = FirstNonEmpty(payload.Notes, payload.TwoFaNote);
        var snippet = TrimSnippet(snippetSource);
        return new AllItemEntry(
            row.Header.Id,
            row.Header.Type,
            payload.Title,
            secondary,
            snippet,
            labels,
            BuildSearchText(payload.Title, secondary, snippetSource, string.Join(" ", labels)),
            row.Header.CreatedAtUtc,
            row.Header.UpdatedAtUtc);
    }

    private AllItemEntry BuildCardEntry(VaultItemRow row, IReadOnlyList<string> labels)
    {
        var json = AesGcmBlob.Decrypt(_root.VaultKey, row.EncryptedPayload);
        var payload = JsonSerializer.Deserialize<CardPayload>(json, JsonOpts)
            ?? new CardPayload("", "", "", 0, 0, "", "");

        var secondary = string.IsNullOrWhiteSpace(payload.Cardholder)
            ? MaskCardNumber(payload.Number)
            : $"{payload.Cardholder} / {MaskCardNumber(payload.Number)}";
        var snippetSource = string.IsNullOrWhiteSpace(payload.Notes) ? "" : payload.Notes;
        var snippet = TrimSnippet(snippetSource);

        return new AllItemEntry(
            row.Header.Id,
            row.Header.Type,
            payload.Title,
            secondary,
            snippet,
            labels,
            BuildSearchText(payload.Title, secondary, snippetSource, string.Join(" ", labels), payload.Number, payload.Cvc),
            row.Header.CreatedAtUtc,
            row.Header.UpdatedAtUtc);
    }

    private AllItemEntry BuildNoteEntry(VaultItemRow row, IReadOnlyList<string> labels)
    {
        var json = AesGcmBlob.Decrypt(_root.VaultKey, row.EncryptedPayload);
        var payload = JsonSerializer.Deserialize<NotePayload>(json, JsonOpts)
            ?? new NotePayload("", "");

        var snippetSource = payload.Content;
        var snippet = TrimSnippet(snippetSource);
        return new AllItemEntry(
            row.Header.Id,
            row.Header.Type,
            payload.Title,
            "Secure note",
            snippet,
            labels,
            BuildSearchText(payload.Title, "Secure note", snippetSource, string.Join(" ", labels), payload.Content),
            row.Header.CreatedAtUtc,
            row.Header.UpdatedAtUtc);
    }

    private static string BuildSearchText(params string?[] parts)
        => string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));

    private static string FirstNonEmpty(params string?[] parts)
        => parts.FirstOrDefault(part => !string.IsNullOrWhiteSpace(part)) ?? "";

    private static string TrimSnippet(string text, int maxLength = 96)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        var value = text.Trim();
        if (value.Length <= maxLength)
            return value;

        return value[..(maxLength - 1)].TrimEnd() + "...";
    }

    private static string MaskCardNumber(string number)
    {
        if (string.IsNullOrWhiteSpace(number))
            return "";

        var digits = new string(number.Where(char.IsDigit).ToArray());
        if (digits.Length <= 4)
            return "****";

        return $"**** **** **** {digits[^4..]}";
    }

    private static IReadOnlyList<string> ParseLabelNames(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        return text
            .Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
