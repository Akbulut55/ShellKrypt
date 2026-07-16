using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

namespace ShellKrypt.Desktop.Features.VaultAccess;

public sealed partial class WelcomeViewModel
{
    [RelayCommand(CanExecute = nameof(CanAcceptSecurityAcknowledgement))]
    private async Task AcceptSecurityAcknowledgementAsync()
    {
        var action = _pendingSecurityAcknowledgementAction;
        var vault = _pendingSecurityAcknowledgementVault;

        _settings.AcceptSecurityAcknowledgement();
        ClearSecurityAcknowledgement();

        switch (action)
        {
            case SecurityAcknowledgementAction.CreateVault:
                _navigation.GoCreateVault();
                break;
            case SecurityAcknowledgementAction.ImportVault:
                await ImportVaultAsync();
                break;
            case SecurityAcknowledgementAction.OpenVault:
                OpenVault(vault);
                break;
        }
    }

    [RelayCommand]
    private void CancelSecurityAcknowledgement()
    {
        ClearSecurityAcknowledgement();
    }

    private bool RequestSecurityAcknowledgement(SecurityAcknowledgementAction action, VaultRecordVm? vault = null)
    {
        if (_settings.HasAcceptedSecurityAcknowledgement)
            return false;

        _pendingSecurityAcknowledgementAction = action;
        _pendingSecurityAcknowledgementVault = vault;
        SecurityAcknowledgementConfirmed = false;
        IsSecurityAcknowledgementOpen = true;
        return true;
    }

    private void ClearSecurityAcknowledgement()
    {
        IsSecurityAcknowledgementOpen = false;
        SecurityAcknowledgementConfirmed = false;
        _pendingSecurityAcknowledgementAction = SecurityAcknowledgementAction.None;
        _pendingSecurityAcknowledgementVault = null;
    }
}
