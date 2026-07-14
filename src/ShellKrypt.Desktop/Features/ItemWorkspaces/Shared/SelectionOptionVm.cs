namespace ShellKrypt.Desktop.Features.ItemWorkspaces.Shared;

public sealed record SelectionOptionVm(string Key, string Label)
{
    public override string ToString() => Label;
}
