using System;
using Avalonia;
using Avalonia.Controls;

namespace ShellKrypt.Desktop.Features.ItemWorkspaces.Shared;

public sealed class ResponsiveItemDetailsPanel : Panel
{
    public static readonly StyledProperty<double> BreakpointProperty =
        AvaloniaProperty.Register<ResponsiveItemDetailsPanel, double>(nameof(Breakpoint), 760);

    public static readonly StyledProperty<double> IdentityWidthProperty =
        AvaloniaProperty.Register<ResponsiveItemDetailsPanel, double>(nameof(IdentityWidth), 360);

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<ResponsiveItemDetailsPanel, double>(nameof(Gap), 28);

    public double Breakpoint { get => GetValue(BreakpointProperty); set => SetValue(BreakpointProperty, value); }
    public double IdentityWidth { get => GetValue(IdentityWidthProperty); set => SetValue(IdentityWidthProperty, value); }
    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count == 0)
            return default;

        var width = double.IsInfinity(availableSize.Width) ? Breakpoint : availableSize.Width;
        if (UseColumns(width) && Children.Count >= 2)
        {
            var leftWidth = Math.Min(IdentityWidth, Math.Max(0, width * 0.38));
            var rightWidth = Math.Max(0, width - leftWidth - Gap);
            Children[0].Measure(new Size(leftWidth, double.PositiveInfinity));
            Children[1].Measure(new Size(rightWidth, double.PositiveInfinity));
            MeasureRemaining(rightWidth);
            return new Size(width, Math.Max(Children[0].DesiredSize.Height, Children[1].DesiredSize.Height));
        }

        var height = 0d;
        for (var index = 0; index < Children.Count; index++)
        {
            Children[index].Measure(new Size(width, double.PositiveInfinity));
            height += Children[index].DesiredSize.Height;
            if (index > 0)
                height += Gap;
        }

        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (UseColumns(finalSize.Width) && Children.Count >= 2)
        {
            var leftWidth = Math.Min(IdentityWidth, Math.Max(0, finalSize.Width * 0.38));
            var rightWidth = Math.Max(0, finalSize.Width - leftWidth - Gap);
            Children[0].Arrange(new Rect(0, 0, leftWidth, finalSize.Height));
            Children[1].Arrange(new Rect(leftWidth + Gap, 0, rightWidth, finalSize.Height));
            for (var index = 2; index < Children.Count; index++)
                Children[index].Arrange(new Rect(leftWidth + Gap, 0, rightWidth, finalSize.Height));
            return finalSize;
        }

        var y = 0d;
        foreach (var child in Children)
        {
            child.Arrange(new Rect(0, y, finalSize.Width, child.DesiredSize.Height));
            y += child.DesiredSize.Height + Gap;
        }

        return finalSize;
    }

    private bool UseColumns(double width) => width >= Breakpoint;

    private void MeasureRemaining(double width)
    {
        for (var index = 2; index < Children.Count; index++)
            Children[index].Measure(new Size(width, double.PositiveInfinity));
    }
}
