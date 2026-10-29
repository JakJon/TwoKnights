using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Keeps a controller's selection on screen in a scrolling list without dragging
/// the whole list along one row per press.
///
/// ScrollView.ScrollTo moves the least it can, so once the selection reaches the
/// bottom edge every further press shifts the list by one row. Reveal waits until
/// the selection would leave the view, then scrolls a few rows past it in one go:
/// the selection still moves one row, but the next few presses move only the
/// highlight while the list stays put.
/// </summary>
public static class ListScroll
{
    // How many rows beyond the selection come into view when the list does move,
    // measured in the selected row's own height
    private const float RowsAhead = 3f;

    public static void Reveal(ScrollView view, VisualElement item)
    {
        if (view == null || item == null) return;

        float viewHeight = view.contentViewport.layout.height;
        Rect box = view.contentContainer.WorldToLocal(item.worldBound);

        // Before the first layout pass there is nothing to measure yet
        if (float.IsNaN(viewHeight) || viewHeight <= 0f || float.IsNaN(box.yMin) || float.IsNaN(box.yMax))
        {
            view.ScrollTo(item);
            return;
        }

        // Never more than half a view ahead, so a short list or a tall row can't
        // push the selection off the opposite edge
        float ahead = Mathf.Min(box.height * RowsAhead, Mathf.Max(0f, viewHeight - box.height) * 0.5f);

        Vector2 offset = view.scrollOffset;
        float top = offset.y;
        float bottom = top + viewHeight;

        if (box.yMax > bottom) offset.y = box.yMax - viewHeight + ahead;
        else if (box.yMin < top) offset.y = box.yMin - ahead;
        else return; // already in view: the highlight moves, the list does not

        float max = view.verticalScroller.highValue;
        if (float.IsNaN(max) || max < 0f) max = 0f;
        offset.y = Mathf.Clamp(offset.y, 0f, max);
        view.scrollOffset = offset;
    }
}
