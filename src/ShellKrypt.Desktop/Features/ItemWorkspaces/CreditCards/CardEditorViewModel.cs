using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Core.Items;
using ShellKrypt.Desktop.Features.ItemWorkspaces.Shared;
using ShellKrypt.Desktop.ViewModels;
using ShellKrypt.UI.Shared.Controls;

namespace ShellKrypt.Desktop.Features.ItemWorkspaces.CreditCards;

public partial class CardEditorViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _root; private readonly ICardService _service; private readonly Func<CardEntry?, string?, Task> _onMutation; private readonly Func<string?, Task> _refreshAllItems; private CardRowVm? _selected; private bool _formatting;
    public IReadOnlyList<string> IssuerOptions { get; } = ["Visa", "Mastercard", "Amex", "Discover", "JCB", "UnionPay", "Diners Club", "Card"];
    public IReadOnlyList<string> CardTypeOptions { get; } = ["Credit Card", "Debit Card", "Bank Card", "Prepaid Card", "Virtual Card", "Charge Card"];
    [ObservableProperty] private bool isOpen; [ObservableProperty] private ItemEditorMode mode; [ObservableProperty] private string title = ""; [ObservableProperty] private string bank = ""; [ObservableProperty] private string cardholder = ""; [ObservableProperty] private string issuer = "Card"; [ObservableProperty] private string cardType = "Credit Card"; [ObservableProperty] private string number = ""; [ObservableProperty] private string expiryMonth = ""; [ObservableProperty] private string expiryYear = ""; [ObservableProperty] private string cvc = ""; [ObservableProperty] private string notes = ""; [ObservableProperty] private bool secretsVisible; [ObservableProperty] private string error = "";
    public CardEditorViewModel(MainWindowViewModel root, ICardService service, Func<CardEntry?, string?, Task> onMutation, Func<string?, Task> refreshAllItems) { _root = root; _service = service; _onMutation = onMutation; _refreshAllItems = refreshAllItems; }
    public bool IsAdd => Mode == ItemEditorMode.Add; public bool IsDetails => Mode == ItemEditorMode.Details; public bool IsEdit => Mode == ItemEditorMode.Edit; public bool IsConfirmDelete => Mode == ItemEditorMode.ConfirmDelete; public bool IsEditable => IsAdd || IsEdit;
    public string NumberDisplay => SecretsVisible ? CardRowVm.FormatCardNumber(Number) : Number.Length == 0 ? "" : $"**** **** **** {CardRowVm.DigitsOnly(Number, 19).TakeLast(4).Aggregate("", (text, ch) => text + ch)}";
    public string CvcDisplay => SecretsVisible ? Cvc : Cvc.Length == 0 ? "" : "***";
    public string CardPreviewNumber => Number.Length == 0 ? "••••  ••••  ••••  ••••" : $"••••  ••••  ••••  {CardRowVm.DigitsOnly(Number, 19).TakeLast(4).Aggregate("", (text, ch) => text + ch)}";
    public string CardPreviewExpiry => $"{(string.IsNullOrWhiteSpace(ExpiryMonth) ? "MM" : ExpiryMonth)}/{FormatShortYear(ExpiryYear)}";
    public string NotesDisplay => string.IsNullOrWhiteSpace(Notes) ? T(_root, "ItemWorkspace.Details.NoNotes") : Notes.Trim();
    public ModalShellSize ModalSize => IsDetails ? ModalShellSize.ItemDetails : ModalShellSize.Wide;
    public string ModalTitle => IsAdd ? T(_root, "Cards.Modal.AddTitle") : IsEdit ? T(_root, "Cards.Modal.EditTitle") : IsConfirmDelete ? T(_root, "Cards.Modal.DeleteTitle") : T(_root, "Cards.Modal.DetailsTitle");
    public string ModalSubtitle => IsAdd ? T(_root, "Cards.Modal.AddSubtitle") : IsEdit ? T(_root, "Cards.Modal.EditSubtitle") : IsConfirmDelete ? T(_root, "Cards.Modal.DeleteSubtitle") : T(_root, "ItemWorkspace.Details.StoredLocally");
    public string FooterText => IsDetails ? "" : IsConfirmDelete ? T(_root, "Cards.Modal.DeleteFooter", Title) : T(_root, "Cards.Modal.Footer");
    partial void OnModeChanged(ItemEditorMode value) { if (value != ItemEditorMode.Details) SecretsVisible = false; NotifyMode(); } partial void OnSecretsVisibleChanged(bool value) { OnPropertyChanged(nameof(NumberDisplay)); OnPropertyChanged(nameof(CvcDisplay)); }
    partial void OnNumberChanged(string value) { if (_formatting) return; var formatted = CardRowVm.FormatCardNumber(value, CardRowVm.StandardCardNumberMaxDigits, true); if (formatted != value) { _formatting = true; Number = formatted; _formatting = false; } var detected = CardRowVm.DetectIssuer(formatted); if (Issuer == "Card" || string.IsNullOrWhiteSpace(Issuer)) Issuer = detected; OnPropertyChanged(nameof(NumberDisplay)); OnPropertyChanged(nameof(CardPreviewNumber)); }
    partial void OnCvcChanged(string value) { var normalized = CardRowVm.DigitsOnly(value, 4); if (normalized != value) Cvc = normalized; OnPropertyChanged(nameof(CvcDisplay)); }
    partial void OnExpiryMonthChanged(string value) { var normalized = CardRowVm.DigitsOnly(value, 2); if (normalized != value) ExpiryMonth = normalized; OnPropertyChanged(nameof(CardPreviewExpiry)); }
    partial void OnExpiryYearChanged(string value) { var normalized = CardRowVm.DigitsOnly(value, 4); if (normalized != value) ExpiryYear = normalized; OnPropertyChanged(nameof(CardPreviewExpiry)); }
    partial void OnNotesChanged(string value) => OnPropertyChanged(nameof(NotesDisplay));
    public void OpenAdd() { _selected = null; Clear(); Mode = ItemEditorMode.Add; IsOpen = true; }
    public void OpenDetails(CardRowVm row) { _selected = row; Populate(row); Mode = ItemEditorMode.Details; IsOpen = true; }
    [RelayCommand] private void Close() { IsOpen = false; _selected = null; Clear(); }
    [RelayCommand] private void BeginEdit() => Mode = ItemEditorMode.Edit; [RelayCommand] private void BeginDelete() => Mode = ItemEditorMode.ConfirmDelete; [RelayCommand] private void CancelDelete() => Mode = ItemEditorMode.Details;
    [RelayCommand] private void CancelEdit() { if (IsAdd) Close(); else { if (_selected is not null) Populate(_selected); Mode = ItemEditorMode.Details; } }
    [RelayCommand] private void Cancel() { if (IsConfirmDelete) CancelDelete(); else CancelEdit(); }
    [RelayCommand] private void ToggleSecrets() => SecretsVisible = !SecretsVisible;
    [RelayCommand] private async Task CopyNumberAsync() { if (Number.Length > 0) await _root.CopyToClipboardAsync(CardRowVm.DigitsOnly(Number, 19)); }
    [RelayCommand] private async Task SaveAsync()
    { Error = ""; if (_root.VaultPath is null) { Error = T(_root, "Common.NoVaultSelected"); return; } if (string.IsNullOrWhiteSpace(Title)) { Error = T(_root, "Validation.TitleRequired"); return; } var digits = CardRowVm.DigitsOnly(Number, 16); if (digits.Length < 12) { Error = T(_root, "Cards.Error.CardNumberTooShort"); return; } if (!int.TryParse(ExpiryMonth, out var month) || month is < 1 or > 12) { Error = T(_root, "Cards.Error.ExpiryMonth"); return; } if (!int.TryParse(ExpiryYear, out var year) || year is < 2000 or > 2100) { Error = T(_root, "Cards.Error.ExpiryYear"); return; } var cvc = CardRowVm.DigitsOnly(Cvc, 4); if (cvc.Length is < 3 or > 4) { Error = T(_root, "Cards.Error.Cvc"); return; } try { var input = new CardInput(Title, Bank, Cardholder, digits, month, year, cvc, Notes, Issuer, CardType); var entry = _selected is null ? await _service.AddAsync(_root.VaultPath, _root.VaultKey, input) : await _service.UpdateAsync(_root.VaultPath, _root.VaultKey, _selected.Id, _selected.CreatedAtUtc, input); await _refreshAllItems(entry.Id); await _onMutation(entry, null); _root.LogActivity("cards", _selected is null ? "Credit card added" : "Credit card updated", $"{(_selected is null ? "Added" : "Updated")} {entry.Title}.", _selected is null ? "success" : "info", affectedItem: entry.Title); _selected = new CardRowVm(_root.Localization, entry.Id, entry.Title, entry.Bank, entry.Cardholder, entry.Number, entry.ExpiryMonth.ToString("00"), entry.ExpiryYear.ToString(), entry.Cvc, entry.Notes, entry.Issuer, entry.CardType, entry.CreatedAtUtc, entry.UpdatedAtUtc); Populate(_selected); Mode = ItemEditorMode.Details; } catch (Exception ex) { Error = ex.Message; } }
    [RelayCommand] private async Task ConfirmDeleteAsync() { if (_selected is null || _root.VaultPath is null) return; try { var row = _selected; await _service.DeleteAsync(_root.VaultPath, row.Id); await _refreshAllItems(null); await _onMutation(null, row.Id); _root.LogActivity("cards", "Credit card deleted", $"Deleted {row.Title}.", "warning", affectedItem: row.Title); Close(); } catch (Exception ex) { Error = ex.Message; } }
    private void Populate(CardRowVm row) { Title = row.Title; Bank = row.Bank; Cardholder = row.Cardholder; Issuer = row.Issuer; CardType = row.CardType; Number = row.Number; ExpiryMonth = row.ExpiryMonth; ExpiryYear = row.ExpiryYear; Cvc = row.Cvc; Notes = row.Notes; SecretsVisible = false; Error = ""; }
    private void Clear() { Title = Bank = Cardholder = Number = ExpiryMonth = ExpiryYear = Cvc = Notes = Error = ""; Issuer = "Card"; CardType = "Credit Card"; SecretsVisible = false; }
    private static string FormatShortYear(string value) { var text = value?.Trim() ?? ""; return text.Length >= 2 ? text[^2..] : text.Length == 0 ? "YY" : text; }
    private void NotifyMode() { OnPropertyChanged(nameof(IsAdd)); OnPropertyChanged(nameof(IsDetails)); OnPropertyChanged(nameof(IsEdit)); OnPropertyChanged(nameof(IsConfirmDelete)); OnPropertyChanged(nameof(IsEditable)); OnPropertyChanged(nameof(ModalSize)); OnPropertyChanged(nameof(ModalTitle)); OnPropertyChanged(nameof(ModalSubtitle)); OnPropertyChanged(nameof(FooterText)); OnPropertyChanged(nameof(NotesDisplay)); }
    public override void RefreshLocalization() => NotifyMode();
}
