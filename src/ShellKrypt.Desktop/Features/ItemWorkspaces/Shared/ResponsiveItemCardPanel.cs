using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;

namespace ShellKrypt.Desktop.Features.ItemWorkspaces.Shared;

public sealed class ResponsiveItemCardPanel : Panel
{
    public static readonly StyledProperty<double> MinimumCardWidthProperty =
        AvaloniaProperty.Register<ResponsiveItemCardPanel, double>(nameof(MinimumCardWidth), 320);

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<ResponsiveItemCardPanel, double>(nameof(Gap), 12);

    public static readonly StyledProperty<int> MaximumColumnsProperty =
        AvaloniaProperty.Register<ResponsiveItemCardPanel, int>(nameof(MaximumColumns), 3);

    public double MinimumCardWidth
    {
        get => GetValue(MinimumCardWidthProperty);
        set => SetValue(MinimumCardWidthProperty, value);
    }

    public double Gap
    {
        get => GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    public int MaximumColumns
    {
        get => GetValue(MaximumColumnsProperty);
        set => SetValue(MaximumColumnsProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width)
            ? Math.Max(MinimumCardWidth, Children.Select(child => child.DesiredSize.Width).DefaultIfEmpty().Max())
            : availableSize.Width;
        var columns = CalculateColumns(width);
        var cardWidth = Math.Max(0, (width - Gap * (columns - 1)) / columns);
        var rowHeights = new List<double>();

        for (var i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            child.Measure(new Size(cardWidth, double.PositiveInfinity));
            var row = i / columns;
            if (row == rowHeights.Count)
                rowHeights.Add(child.DesiredSize.Height);
            else
                rowHeights[row] = Math.Max(rowHeights[row], child.DesiredSize.Height);
        }

        return new Size(width, rowHeights.Sum() + Math.Max(0, rowHeights.Count - 1) * Gap);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = CalculateColumns(finalSize.Width);
        var cardWidth = Math.Max(0, (finalSize.Width - Gap * (columns - 1)) / columns);
        var rowHeights = new List<double>();

        for (var i = 0; i < Children.Count; i++)
        {
            var row = i / columns;
            if (row == rowHeights.Count)
                rowHeights.Add(Children[i].DesiredSize.Height);
            else
                rowHeights[row] = Math.Max(rowHeights[row], Children[i].DesiredSize.Height);
        }

        var y = 0d;
        for (var i = 0; i < Children.Count; i++)
        {
            var row = i / columns;
            var column = i % columns;
            if (column == 0 && row > 0)
                y += rowHeights[row - 1] + Gap;

            Children[i].Arrange(new Rect(column * (cardWidth + Gap), y, cardWidth, rowHeights[row]));
        }

        return finalSize;
    }

    private int CalculateColumns(double width)
    {
        if (width <= 0 || double.IsInfinity(width))
            return 1;

        var count = (int)Math.Floor((width + Gap) / (Math.Max(1, MinimumCardWidth) + Gap));
        return Math.Clamp(count, 1, Math.Max(1, MaximumColumns));
    }
}
