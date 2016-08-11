using System;
using System.Collections.Generic;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI.Timeline.Components
{
	public class RollNodeView : IRollWidget
	{
		readonly Row row;
		readonly NodeRow nodeData;
		readonly SimpleText label;
		readonly EditBox editBox;
		readonly Image nodeIcon;
		readonly Widget widget;

		public RollNodeView(Row row, int indentation)
		{
			this.row = row;
			nodeData = row.Components.Get<NodeRow>();
			label = new SimpleText { HitTestTarget = true, LayoutCell = new LayoutCell(Alignment.Center) };
			editBox = new EditBox { AutoSizeConstraints = false, LayoutCell = new LayoutCell(Alignment.Center, stretchX: 1000) };
			nodeIcon = new Image {
				LayoutCell = new LayoutCell { Alignment = Alignment.Center },
				Texture = IconPool.GetTexture("Nodes." + nodeData.Node.GetType(), "Nodes.Unknown"),
			};
			nodeIcon.MinMaxSize = (Vector2)nodeIcon.Texture.ImageSize;
			var expandButtonContainer = new Widget {
				Layout = new StackLayout { IgnoreHidden = false },
				LayoutCell = new LayoutCell(Alignment.Center),
				Nodes = { CreateExpandButton() }
			};
			widget = new Widget {
				Padding = new Thickness { Left = 4, Right = 2 },
				MinHeight = Metrics.TimelineDefaultRowHeight,
				Layout = new HBoxLayout(),
				HitTestTarget = true,
				Nodes = {
					new HSpacer(indentation * Metrics.TimelineRollIndentation),
					expandButtonContainer,
					new HSpacer(3),
					nodeIcon,
					new HSpacer(3),
					label,
					editBox,
					new Widget(),
					CreateEyeButton(),
					CreateLockButton(),
				},
			};
			widget.CompoundPresenter.Push(new DelegatePresenter<Widget>(RenderBackground));
			editBox.Visible = false;
			widget.Tasks.Add(MonitorDoubleClickTask());
		}

		BitmapButton CreateEyeButton()
		{
			var button = new BitmapButton(Metrics.IconSize) { LayoutCell = new LayoutCell(Alignment.Center) };
			button.Tasks.Add(new Property<NodeVisibility>(() => nodeData.Visibility).DistinctUntilChanged().Consume(i => {
				var texture = "Timeline.Dot";
				if (i == NodeVisibility.Show) {
					texture = "Timeline.Eye";
				} else if (i == NodeVisibility.Hide) {
					texture = "Timeline.Cross";
				}
				button.DefaultTexture = IconPool.GetTexture(texture);
			}));
			button.Clicked += () => {
				Core.Operations.SetGenericProperty<NodeVisibility>.Perform(
					() => nodeData.Visibility, value => nodeData.Visibility = value,
					(NodeVisibility)(((int)nodeData.Visibility + 1) % 3)
				);
			};
			return button;
		}

		BitmapButton CreateLockButton()
		{
			var button = new BitmapButton(Metrics.IconSize) { LayoutCell = new LayoutCell(Alignment.Center) };
			button.Tasks.Add(new Property<bool>(() => nodeData.Locked).DistinctUntilChanged().Consume(i => {
				button.DefaultTexture = IconPool.GetTexture(i ? "Timeline.Lock" : "Timeline.Dot");
			}));
			button.Clicked += () => {
				Core.Operations.SetGenericProperty<bool>.Perform(() => nodeData.Locked, value => nodeData.Locked = value, !nodeData.Locked);
			};
			return button;
		}

		BitmapButton CreateExpandButton()
		{
			var button = new BitmapButton(Metrics.IconSize) { LayoutCell = new LayoutCell(Alignment.Center) };
			button.Tasks.Add(new Property<bool>(() => nodeData.Expanded).DistinctUntilChanged().Consume(i => {
				button.DefaultTexture = IconPool.GetTexture(i ? "Timeline.Expanded" : "Timeline.Collapsed");
			}));
			button.Clicked += () => {
				Core.Operations.SetGenericProperty<bool>.Perform(() => nodeData.Expanded, value => nodeData.Expanded = value, !nodeData.Expanded);
			};
			button.Updated += delta => button.Visible = nodeData.Node.Animators.Count > 0;
			return button;
		}

		void RenderBackground(Widget widget)
		{
			widget.PrepareRendererState();
			Renderer.DrawRect(Vector2.Zero, widget.Size, Timeline.Instance.SelectedRows.Contains(row) ? Colors.SelectedBackground : Colors.WhiteBackground);
		}

		Widget IRollWidget.Widget => widget;

		IEnumerator<object> MonitorDoubleClickTask()
		{
			while (true) {
				label.Text = nodeData.Node.Id;
				if (widget.Input.WasKeyPressed(Key.Mouse0DoubleClick)) {
					if (label.IsMouseOver()) {
						Operations.ClearRowSelection.Perform();
						Operations.SelectRow.Perform(row);
						label.Visible = false;
						editBox.Visible = true;
						editBox.Text = label.Text;
						yield return null;
						editBox.SetFocus();
						editBox.Tasks.Add(EditNodeIdTask());
					} else if (widget.IsMouseOver()) {
						Operations.SetCurrentContainer.Perform(row.Components.Get<NodeRow>().Node);
					}
				}
				yield return null;
			}
		}

		IEnumerator<object> EditNodeIdTask()
		{
			editBox.Input.CaptureMouse();
			var initialText = editBox.Text;
			while (editBox.IsFocused()) {
				yield return null;
			}
			editBox.Input.ReleaseMouse();
			editBox.Visible = false;
			label.Visible = true;
			if (editBox.Text != initialText) {
				Core.Operations.SetProperty.Perform(nodeData.Node, "Id", editBox.Text);
			}
		}
	}
}
