using System.Windows.Input;
using ShellKrypt.Core.Items;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class AllItemsViewModel
{
    private void AddItem()
    {
        Error = string.Empty;

        var targetType = ActiveType switch
        {
            "web" => ItemType.Web,
            "card" => ItemType.Card,
            "note" => ItemType.Note,
            "authenticator" => ItemType.Authenticator,
            "api" => ItemType.ApiKey,
            "project" => ItemType.ProjectSecret,
            _ => SelectedRow?.Type ?? ItemType.Web
        };

        switch (targetType)
        {
            case ItemType.Web:
                _shell.ShowWebLogins();
                ExecuteCommand(_shell.WebLogins.AddNewCommand);
                break;

            case ItemType.Card:
                _shell.ShowCards();
                ExecuteCommand(_shell.Cards.AddNewCommand);
                break;

            case ItemType.Note:
                _shell.ShowMarkdownNotes();
                ExecuteCommand(_shell.MarkdownNotes.NewNoteCommand);
                break;

            case ItemType.Authenticator:
                _shell.ShowAuthenticator();
                ExecuteCommand(_shell.Authenticator.AddNewCommand);
                break;

            case ItemType.ApiKey:
                _shell.ShowApiKeys();
                ExecuteCommand(_shell.ApiKeys.AddNewCommand);
                break;
            case ItemType.ProjectSecret:
                _shell.ShowProjectSecrets();
                ExecuteCommand(_shell.ProjectSecrets.AddProjectCommand);
                break;
        }
    }

    private void OpenRow(AllItemEntry? row)
    {
        if (row is null)
            return;

        Error = string.Empty;

        switch (row.Type)
        {
            case ItemType.Web:
                _shell.ShowWebLogins();
                break;
            case ItemType.Card:
                _shell.ShowCards();
                break;
            case ItemType.Note:
                _shell.ShowMarkdownNotes();
                break;
            case ItemType.Authenticator:
                _shell.ShowAuthenticator();
                _ = _shell.ShowAuthenticatorByIdAsync(row.Id);
                break;
            case ItemType.ApiKey:
                _shell.ShowApiKeys();
                _ = _shell.ShowApiKeyByIdAsync(row.Id);
                break;
            case ItemType.ProjectSecret:
                _shell.ShowProjectSecrets();
                _ = _shell.ShowProjectSecretByIdAsync(row.Id);
                break;
        }
    }

    private static void ExecuteCommand(ICommand? command)
    {
        if (command is not null && command.CanExecute(null))
            command.Execute(null);
    }
}
