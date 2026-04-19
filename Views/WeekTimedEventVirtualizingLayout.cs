using GoogleCalendarManagement.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace GoogleCalendarManagement.Views;

internal sealed class WeekTimedEventVirtualizingLayout : VirtualizingLayout
{
    private readonly Dictionary<int, UIElement> _realizedElements = [];

    public string? DragGcalEventId { get; set; }
    public double DragHeight { get; set; }

    protected override void UninitializeForContextCore(VirtualizingLayoutContext context)
    {
        foreach (var element in _realizedElements.Values)
        {
            context.RecycleElement(element);
        }

        _realizedElements.Clear();
        base.UninitializeForContextCore(context);
    }

    protected override Size MeasureOverride(VirtualizingLayoutContext context, Size availableSize)
    {
        var visibleIndices = new HashSet<int>();
        var realizationRect = context.RealizationRect;
        double extentWidth = 0;
        double extentHeight = 0;

        for (var index = 0; index < context.ItemCount; index++)
        {
            if (context.GetItemAt(index) is not WeekTimedEventLayoutItem item)
            {
                continue;
            }

            if (!TryGetValidBounds(item, out var bounds))
            {
                continue;
            }

            extentWidth = Math.Max(extentWidth, bounds.Right);
            extentHeight = Math.Max(extentHeight, bounds.Bottom);

            if (!Intersects(bounds, realizationRect))
            {
                continue;
            }

            visibleIndices.Add(index);
            var element = context.GetOrCreateElementAt(index);
            element.Measure(new Size(bounds.Width, bounds.Height));
            _realizedElements[index] = element;
        }

        foreach (var (index, element) in _realizedElements.ToArray())
        {
            if (visibleIndices.Contains(index))
            {
                continue;
            }

            context.RecycleElement(element);
            _realizedElements.Remove(index);
        }

        return new Size(
            NormalizeExtent(availableSize.Width, extentWidth),
            NormalizeExtent(availableSize.Height, extentHeight));
    }

    protected override Size ArrangeOverride(VirtualizingLayoutContext context, Size finalSize)
    {
        foreach (var (index, element) in _realizedElements)
        {
            if (context.GetItemAt(index) is not WeekTimedEventLayoutItem item ||
                !TryGetValidBounds(item, out var bounds))
            {
                continue;
            }

            element.Arrange(bounds);
        }

        return finalSize;
    }

    private static double NormalizeExtent(double availableSize, double extent)
    {
        var safeExtent = double.IsFinite(extent) && extent >= 0 ? extent : 0;
        if (!double.IsFinite(availableSize))
        {
            return safeExtent;
        }

        return Math.Max(0, Math.Max(availableSize, safeExtent));
    }

    private bool TryGetValidBounds(WeekTimedEventLayoutItem item, out Rect bounds)
    {
        var height = DragGcalEventId is not null &&
            string.Equals(item.GcalEventId, DragGcalEventId, StringComparison.Ordinal) &&
            DragHeight > 0
            ? DragHeight
            : item.Height;

        if (!double.IsFinite(item.Left) ||
            !double.IsFinite(item.Top) ||
            !double.IsFinite(item.Width) ||
            !double.IsFinite(height) ||
            item.Width <= 0 ||
            height <= 0)
        {
            bounds = Rect.Empty;
            return false;
        }

        bounds = new Rect(item.Left, item.Top, item.Width, height);
        return true;
    }

    private static bool Intersects(Rect bounds, Rect realizationRect)
    {
        var itemRight = bounds.X + bounds.Width;
        var itemBottom = bounds.Y + bounds.Height;
        var realizationRight = realizationRect.X + realizationRect.Width;
        var realizationBottom = realizationRect.Y + realizationRect.Height;

        return bounds.X < realizationRight &&
            itemRight > realizationRect.X &&
            bounds.Y < realizationBottom &&
            itemBottom > realizationRect.Y;
    }
}
