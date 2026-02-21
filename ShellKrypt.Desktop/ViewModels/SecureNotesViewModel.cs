using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Core.Items;
using ShellKrypt.Infrastructure.Crypto;
using System;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Threading.Tasks;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class NoteItemVm : ObservableObject
{
    public string Id { get; }
    public string CreatedAtUtc { get; }
    public string UpdatedAtUtc { get; private set; }

    [ObservableProperty] private string title;
    [ObservableProperty] private string content;

    public NoteItemVm(string id, string title, string content, string createdAtUtc, string updatedAtUtc)
    {
        Id = id;
        Title = title;
        Content = content;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void TouchUpdated() => UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O");
}

public partial class SecureNotesViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _root;
    private readonly IItemRepository _repo;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ObservableCollection<NoteItemVm> Notes { get; } = new();

    [ObservableProperty] private NoteItemVm? selectedNote;
    [ObservableProperty] private string editorTitle = "";
    [ObservableProperty] private string editorContent = "";
    [ObservableProperty] private string error = "";
    [ObservableProperty] private bool isBusy;

    public SecureNotesViewModel(MainWindowViewModel root, IItemRepository repo)
    {
        _root = root;
        _repo = repo;
        _ = LoadAsync();
    }

    partial void OnSelectedNoteChanged(NoteItemVm? value)
    {
        if (value is null)
        {
            EditorTitle = "";
            EditorContent = "";
            return;
        }

        EditorTitle = value.Title;
        EditorContent = value.Content;
    }

    [RelayCommand] private void Lock() => _root.Lock();

    [RelayCommand]
    private void NewNote()
    {
        SelectedNote = null;
        EditorTitle = "";
        EditorContent = "";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        Error = "";

        if (_root.VaultPath is null) { Error = "No vault selected."; return; }
        if (string.IsNullOrWhiteSpace(EditorTitle)) { Error = "Title is required."; return; }

        IsBusy = true;
        try
        {
            var now = DateTimeOffset.UtcNow.ToString("O");
            var vaultKey = _root.VaultKey;

            if (SelectedNote is null)
            {
                var id = Guid.NewGuid().ToString("N");
                var payload = new NotePayload(EditorTitle, EditorContent);
                var json = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOpts);
                var enc = AesGcmBlob.Encrypt(vaultKey, json);

                var header = new VaultItemHeader(id, ItemType.Note, false, now, now);
                await _repo.InsertAsync(_root.VaultPath, header, enc);

                var vm = new NoteItemVm(id, EditorTitle, EditorContent, now, now);
                Notes.Insert(0, vm);
                SelectedNote = vm;
            }
            else
            {
                var payload = new NotePayload(EditorTitle, EditorContent);
                var json = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOpts);
                var enc = AesGcmBlob.Encrypt(vaultKey, json);

                SelectedNote.Title = EditorTitle;
                SelectedNote.Content = EditorContent;
                SelectedNote.TouchUpdated();

                var header = new VaultItemHeader(
                    SelectedNote.Id, ItemType.Note, false,
                    SelectedNote.CreatedAtUtc, DateTimeOffset.UtcNow.ToString("O")
                );

                await _repo.UpdateAsync(_root.VaultPath, header, enc);
            }
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
    private async Task DeleteAsync()
    {
        Error = "";

        if (_root.VaultPath is null) { Error = "No vault selected."; return; }
        if (SelectedNote is null) { Error = "Select a note first."; return; }

        IsBusy = true;
        try
        {
            var id = SelectedNote.Id;
            await _repo.DeleteAsync(_root.VaultPath, id);

            var idx = Notes.IndexOf(SelectedNote);
            Notes.Remove(SelectedNote);

            SelectedNote = Notes.Count > 0
                ? Notes[Math.Clamp(idx, 0, Notes.Count - 1)]
                : null;
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

    private async Task LoadAsync()
    {
        Error = "";

        if (_root.VaultPath is null) { Error = "No vault selected."; return; }

        IsBusy = true;
        try
        {
            Notes.Clear();

            var vaultKey = _root.VaultKey;
            var rows = await _repo.ListAsync(_root.VaultPath);

            foreach (var row in rows)
            {
                if (row.Header.Type != ItemType.Note)
                    continue;

                var json = AesGcmBlob.Decrypt(vaultKey, row.EncryptedPayload);
                var payload = JsonSerializer.Deserialize<NotePayload>(json, JsonOpts);
                if (payload is null) continue;

                Notes.Add(new NoteItemVm(
                    row.Header.Id,
                    payload.Title,
                    payload.Content,
                    row.Header.CreatedAtUtc,
                    row.Header.UpdatedAtUtc
                ));
            }

            if (Notes.Count > 0)
                SelectedNote = Notes[0];
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
}