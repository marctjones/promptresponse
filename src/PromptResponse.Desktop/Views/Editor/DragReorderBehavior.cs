using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace PromptResponse.Desktop.Views.Editor;

/// <summary>
/// Lightweight drag-to-reorder helper for items inside an
/// <see cref="ItemsControl"/>.
/// </summary>
/// <remarks>
/// Wiring pattern:
/// 1. The item view's drag handle (a "⋮⋮" or "≡" element) calls
///    <see cref="RegisterDragSource"/> with a payload-type string
///    ("prompt", "section", "column", "row", "topSection") and a function
///    that returns the dragged data (typically the bound view-model).
/// 2. The owning <see cref="ItemsControl"/> calls
///    <see cref="RegisterDropTarget"/> with the matching payload-type and
///    an <c>onMove(from, to)</c> callback that performs the reorder via
///    the view-model's typed Move method.
///
/// Avalonia.Headless can't synthesize real drag-drop events, so the
/// drag-drop UX itself isn't end-to-end testable here — the underlying VM
/// Move methods are unit-tested separately.
///
/// Avalonia 12: payloads are carried via DataTransfer/DataTransferItem with
/// a per-payload-type DataFormat&lt;object&gt; key — replaces the legacy
/// IDataObject API used in Avalonia 11.
/// </remarks>
public static class DragReorderBehavior
{
    /// <summary>Build a DataFormat for the given payload-type token. Same
    /// token used by both source and target sides keeps drags scoped to
    /// matching lists.</summary>
    private static DataFormat<object> FormatFor(string payloadType) =>
        DataFormat.CreateInProcessFormat<object>($"promptresponse.reorder.{payloadType}");

    /// <summary>
    /// Registers <paramref name="handle"/> as a drag source. On pointer-press
    /// we start a drag whose data is whatever <paramref name="payloadFactory"/>
    /// returns (typically the bound view-model). <paramref name="payloadType"/>
    /// must match the corresponding drop target's value.
    /// </summary>
    public static void RegisterDragSource(Control handle, string payloadType, Func<object?> payloadFactory)
    {
        var format = FormatFor(payloadType);
        handle.PointerPressed += async (_, e) =>
        {
            if (!e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed) return;
            var payload = payloadFactory();
            if (payload is null) return;

            var item = DataTransferItem.Create(format, payload);
            var data = new DataTransfer();
            data.Add(item);
            try
            {
                await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Move);
            }
            catch (InvalidOperationException)
            {
                // A drag is already in progress (e.g., user clicked again
                // mid-drag). Ignore — Avalonia keeps the original alive.
            }
        };
    }

    /// <summary>
    /// Wires <paramref name="itemsControl"/> as a drop target for items of
    /// type <paramref name="payloadType"/>. <paramref name="onMove"/> is
    /// invoked with the source and target indices when a drop completes.
    /// Indices are computed from the visual position of the drop relative to
    /// the item containers.
    /// </summary>
    public static void RegisterDropTarget(
        ItemsControl itemsControl,
        string payloadType,
        Action<int, int> onMove)
    {
        var format = FormatFor(payloadType);
        DragDrop.SetAllowDrop(itemsControl, true);
        itemsControl.AddHandler(DragDrop.DragOverEvent, (_, e) =>
        {
            e.DragEffects = e.DataTransfer != null && e.DataTransfer.Formats.Contains(format)
                ? DragDropEffects.Move
                : DragDropEffects.None;
            e.Handled = true;
        });
        itemsControl.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            if (e.DataTransfer == null) return;
            if (!e.DataTransfer.Formats.Contains(format)) return;
            object? payload = null;
            foreach (var item in e.DataTransfer.Items)
            {
                var raw = item.TryGetRaw(format);
                if (raw is not null) { payload = raw; break; }
            }
            if (payload is null) return;

            var items = (itemsControl.ItemsSource as System.Collections.IEnumerable)?
                .Cast<object>()
                .ToList();
            if (items is null) return;
            var fromIndex = items.IndexOf(payload);
            if (fromIndex < 0) return;

            var toIndex = ComputeDropIndex(itemsControl, e, items.Count);
            if (toIndex < 0) return;
            // Removing-then-inserting at the same nominal index is a no-op,
            // so when dropping below the source position we shift down by one.
            if (toIndex > fromIndex) toIndex = Math.Max(0, toIndex - 1);
            if (toIndex == fromIndex) return;
            onMove(fromIndex, toIndex);
            e.Handled = true;
        });
    }

    private static int ComputeDropIndex(ItemsControl itemsControl, DragEventArgs e, int itemCount)
    {
        // Walk item containers in document order; first one whose vertical
        // mid-line is below the pointer determines the drop index.
        var pos = e.GetPosition(itemsControl);
        var index = 0;
        foreach (var child in itemsControl.GetVisualDescendants().OfType<ContentPresenter>())
        {
            if (child.Parent is not Panel) continue;
            var transform = child.TransformToVisual(itemsControl);
            if (transform is null) continue;
            var topInItems = child.Bounds.TopLeft.Transform(transform.Value).Y;
            var midInItems = topInItems + child.Bounds.Height / 2.0;
            if (pos.Y < midInItems) return index;
            index++;
            if (index >= itemCount) break;
        }
        return itemCount;
    }
}
