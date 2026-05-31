using CommunityToolkit.Mvvm.Input;
using System;

namespace ShellKrypt.Desktop.ViewModels;

public partial class ApiKeysViewModel
{
    [RelayCommand]
    private void AddField()
    {
        FormFields.Add(new ApiKeyFieldRowVm(
            Guid.NewGuid().ToString("N"),
            "",
            DefaultFieldType,
            "",
            isSensitive: true,
            isCopyable: true,
            sortOrder: FormFields.Count));
        NotifyFormFieldsChanged();
    }

    [RelayCommand]
    private void RemoveField(ApiKeyFieldRowVm? field)
    {
        if (field is null)
            return;

        FormFields.Remove(field);
        ResequenceFormFields();
        NotifyFormFieldsChanged();
    }

    [RelayCommand]
    private void ToggleFieldVisibility(ApiKeyFieldRowVm? field)
    {
        if (field is not null)
            field.IsValueVisible = !field.IsValueVisible;
    }

    private void AddDefaultFields()
    {
        FormFields.Add(new ApiKeyFieldRowVm(
            Guid.NewGuid().ToString("N"),
            "API Key",
            DefaultFieldType,
            "",
            isSensitive: true,
            isCopyable: true,
            sortOrder: 0));
    }

    private void ResequenceFormFields()
    {
        for (var i = 0; i < FormFields.Count; i++)
            FormFields[i].SortOrder = i;
    }

    private void NotifyFormFieldsChanged()
    {
        OnPropertyChanged(nameof(FormFields));
    }
}
