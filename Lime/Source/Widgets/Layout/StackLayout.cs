using System;
using System.Linq;
using System.Collections.Generic;

namespace Lime
{
	public class StackLayout : CommonLayout, ILayout
	{
		public StackLayout()
		{
			DebugRectangles = new List<Rectangle>();
		}

		public override void MeasureSizeConstraints(Widget widget)
		{
			var widgets = GetChildren(widget, IgnoreHidden);
			if (widgets.Count == 0) {
				widget.MeasuredMinSize = Vector2.Zero;
				widget.MeasuredMaxSize = Vector2.PositiveInfinity;
				return;
			}
			var minSize = new Vector2(widgets.Max(i => i.EffectiveMinSize.X), widgets.Max(i => i.EffectiveMinSize.Y));
			var maxSize = new Vector2(widgets.Max(i => i.EffectiveMaxSize.X), widgets.Max(i => i.EffectiveMaxSize.Y));
			widget.MeasuredMinSize = minSize + widget.Padding;
			widget.MeasuredMaxSize = maxSize + widget.Padding;
		}

		public override void ArrangeChildren(Widget widget)
		{
			ArrangementValid = true;
			var widgets = GetChildren(widget, IgnoreHidden);
			if (widgets.Count == 0) {
				return;
			}
			DebugRectangles.Clear();
			foreach (var w in widgets) {
				LayoutWidgetWithinCell(w, widget.ContentPosition, widget.ContentSize, DebugRectangles);
			}
		}
	}	
}