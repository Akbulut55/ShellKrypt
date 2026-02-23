using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Core.Items;
using ShellKrypt.Infrastructure.Crypto;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class CardRowVm : ObservableObject
{
    public string Id { get; }
    public bool IsNew { get; private set; }
    public string CreatedAtUtc { get; }
    public string UpdatedAtUtc { get; private set; }

    [ObservableProperty] private string title;
    [ObservableProperty] private string cardholder;
    [ObservableProperty] private string number;
    [ObservableProperty] private string expiryMonth; // "01".."12"
    [ObservableProperty] private string expiryYear;  // "2026"
    [ObservableProperty] private string cvc;

    [ObservableProperty] private bool isEditing;
    [ObservableProperty] private bool isSecretsVisible; // show number + cvc

    private string _origTitle = "";
    private string _origCardholder = "";
    private string _origNumber = "";
    private string _origExpiryMonth = "";
    private string _origExpiryYear = "";
    private string _origCvc = "";

    public CardRowVm(
        string id,
        string title,
        string cardholder,
        string number,
        string expiryMonth,
        string expiryYear,
        string cvc,
        string createdAtUtc,
        string updatedAtUtc,
        bool isNew)
    {
        Id = id;
        Title = title;
        Cardholder = cardholder;
        Number = number;
        ExpiryMonth = expiryMonth;
        ExpiryYear = expiryYear;
        Cvc = cvc;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
        IsNew = isNew;
    }

    public bool IsViewing => !IsEditing;

    public string IconLetter => string.IsNullOrWhiteSpace(Title)
        ? "?"
        : Title.Trim()[0].ToString().ToUpperInvariant();

    public string NumberDisplay
        => IsSecretsVisible ? Number : MaskCardNumber(Number);

    public string CvcDisplay
        => IsSecretsVisible ? Cvc : (string.IsNullOrWhiteSpace(Cvc) ? "" : "•••");

    public string ExpiryDisplay
        => $"{(string.IsNullOrWhiteSpace(ExpiryMonth) ? "MM" : ExpiryMonth)}/{(string.IsNullOrWhiteSpace(ExpiryYear) ? "YYYY" : ExpiryYear)}";

    partial void OnIsEditingChanged(bool value) => OnPropertyChanged(nameof(IsViewing));
    partial void OnTitleChanged(string value) => OnPropertyChanged(nameof(IconLetter));
    partial void OnNumberChanged(string value) => OnPropertyChanged(nameof(NumberDisplay));
    partial void OnCvcChanged(string value) => OnPropertyChanged(nameof(CvcDisplay));
    partial void OnExpiryMonthChanged(string value) => OnPropertyChanged(nameof(ExpiryDisplay));
    partial void OnExpiryYearChanged(string value) => OnPropertyChanged(nameof(ExpiryDisplay));
    partial void OnIsSecretsVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(NumberDisplay));
        OnPropertyChanged(nameof(CvcDisplay));
    }

    public void BeginEdit()
    {
        _origTitle = Title;
        _origCardholder = Cardholder;
        _origNumber = Number;
        _origExpiryMonth = ExpiryMonth;
        _origExpiryYear = ExpiryYear;
        _origCvc = Cvc;
        IsEditing = true;
    }

    public void CancelEdit(bool removeIfNew, Action<CardRowVm> removeRow)
    {
        if (removeIfNew && IsNew)
        {
            removeRow(this);
            return;
        }

        Title = _origTitle;
        Cardholder = _origCardholder;
        Number = _origNumber;
        ExpiryMonth = _origExpiryMonth;
        ExpiryYear = _origExpiryYear;
        Cvc = _origCvc;
        IsEditing = false;
    }

    public void MarkSaved()
    {
        IsNew = false;
        IsEditing = false;
        UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O");
    }

    private static string MaskCardNumber(string n)
    {
        if (string.IsNullOrWhiteSpace(n))
            return "";

        var digits = new string(n.Where(char.IsDigit).ToArray());
        if (digits.Length <= 4)
            return "••••";

        var last4 = digits[^4..];
        return $"•••• •••• •••• {last4}";
    }
}

public partial class CardsViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _root;
    private readonly IItemRepository _repo;

    private readonly List<CardRowVm> _all = new();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ObservableCollection<CardRowVm> Rows { get; } = new();

    [ObservableProperty] private string searchText = "";
    [ObservableProperty] private string error = "";

    public CardsViewModel(MainWindowViewModel root, IItemRepository repo)
    {
        _root = root;
        _repo = repo;
        _ = LoadAsync();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    private void AddNew()
    {
        Error = "";
        var now = DateTimeOffset.UtcNow.ToString("O");

        var row = new CardRowVm(
            id: Guid.NewGuid().ToString("N"),
            title: "",
            cardholder: "",
            number: "",
            expiryMonth: "",
            expiryYear: "",
            cvc: "",
            createdAtUtc: now,
            updatedAtUtc: now,
            isNew: true
        );

        row.IsEditing = true;
        _all.Insert(0, row);
        Rows.Insert(0, row);
    }

    [RelayCommand]
    private void BeginEdit(CardRowVm row)
    {
        Error = "";
        row.BeginEdit();
    }

    [RelayCommand]
    private void Cancel(CardRowVm row)
    {
        Error = "";
        row.CancelEdit(removeIfNew: true, removeRow: RemoveRow);
    }

    [RelayCommand]
    private void ToggleSecrets(CardRowVm row)
        => row.IsSecretsVisible = !row.IsSecretsVisible;

    [RelayCommand]
    private async Task SaveAsync(CardRowVm row)
    {
        Error = "";

        if (_root.VaultPath is null) { Error = "No vault selected."; return; }
        if (string.IsNullOrWhiteSpace(row.Title)) { Error = "Title is required."; return; }

        // Basic validation
        var digits = new string((row.Number ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length < 12) { Error = "Card number looks too short."; return; }

        if (!int.TryParse(row.ExpiryMonth, out var mm) || mm < 1 || mm > 12)
        {
            Error = "Expiry month must be 1-12.";
            return;
        }

        if (!int.TryParse(row.ExpiryYear, out var yy) || yy < 2000 || yy > 2100)
        {
            Error = "Expiry year must be like 2026.";
            return;
        }

        var cvcDigits = new string((row.Cvc ?? "").Where(char.IsDigit).ToArray());
        if (cvcDigits.Length is < 3 or > 4)
        {
            Error = "CVC must be 3 or 4 digits.";
            return;
        }

        try
        {
            var now = DateTimeOffset.UtcNow.ToString("O");

            var payload = new CardPayload(
                Title: row.Title,
                Cardholder: row.Cardholder ?? "",
                Number: digits,
                ExpiryMonth: mm,
                ExpiryYear: yy,
                Cvc: cvcDigits,
                Notes: ""
            );

            var json = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOpts);
            var enc = AesGcmBlob.Encrypt(_root.VaultKey, json);

            var header = new VaultItemHeader(
                Id: row.Id,
                Type: ItemType.Card,
                Favorite: false,
                CreatedAtUtc: row.CreatedAtUtc,
                UpdatedAtUtc: now
            );

            if (row.IsNew)
                await _repo.InsertAsync(_root.VaultPath, header, enc);
            else
                await _repo.UpdateAsync(_root.VaultPath, header, enc);

            row.Number = digits;         // normalize
            row.Cvc = cvcDigits;         // normalize
            row.ExpiryMonth = mm.ToString("00");
            row.ExpiryYear = yy.ToString();

            row.MarkSaved();

            // optional: keep filtered view consistent after save
            ApplyFilter();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(CardRowVm row)
    {
        Error = "";
        if (_root.VaultPath is null) { Error = "No vault selected."; return; }

        try
        {
            await _repo.DeleteAsync(_root.VaultPath, row.Id);
            RemoveRow(row);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    private void RemoveRow(CardRowVm row)
    {
        _all.Remove(row);
        Rows.Remove(row);
    }

    private async Task LoadAsync()
    {
        Error = "";
        if (_root.VaultPath is null) { Error = "No vault selected."; return; }

        try
        {
            _all.Clear();
            Rows.Clear();

            var rows = await _repo.ListAsync(_root.VaultPath);

            foreach (var r in rows.Where(x => x.Header.Type == ItemType.Card))
            {
                var json = AesGcmBlob.Decrypt(_root.VaultKey, r.EncryptedPayload);
                var payload = JsonSerializer.Deserialize<CardPayload>(json, JsonOpts);
                if (payload is null) continue;

                _all.Add(new CardRowVm(
                    r.Header.Id,
                    payload.Title,
                    payload.Cardholder,
                    payload.Number,
                    payload.ExpiryMonth.ToString("00"),
                    payload.ExpiryYear.ToString(),
                    payload.Cvc,
                    r.Header.CreatedAtUtc,
                    r.Header.UpdatedAtUtc,
                    isNew: false
                ));
            }

            ApplyFilter();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    private void ApplyFilter()
    {
        Rows.Clear();

        IEnumerable<CardRowVm> filtered = _all;
        var q = SearchText?.Trim();

        if (!string.IsNullOrWhiteSpace(q))
        {
            filtered = filtered.Where(r =>
                r.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (r.Cardholder ?? "").Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.NumberDisplay.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var r in filtered)
            Rows.Add(r);
    }
}