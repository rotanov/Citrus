﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Lime;

namespace Orange
{
	public class OrangeInterface: UserInterface
	{
		private readonly Window window;
		private readonly WindowWidget windowWidget;
		private FileChooser projectPicker;
		private PlatformPicker platformPicker;
		private PluginPanel pluginPanel;
		private ThemedTextView textView;
		private TextWriter textWriter;
		private CheckBoxWithLabel updateVcs;
		private Widget inputLocker = new Widget();

		public OrangeInterface()
		{
			var windowSize = new Vector2(500, 400);
			window = new Window(new WindowOptions {
				ClientSize = windowSize,
				FixedSize = false,
				Title = "Orange"
			});
			window.Closed += The.Workspace.Save;
			windowWidget = new ThemedInvalidableWindowWidget(window) {
				Id = "MainWindow",
				Layout = new HBoxLayout {
					Spacing = 6
				},
				Padding = new Thickness(6),
				Size = windowSize
			};
			var mainVBox = new Widget {
				Layout = new VBoxLayout {
					Spacing = 6
				}
			};
			mainVBox.AddNode(CreateHeaderSection());
			mainVBox.AddNode(CreateVcsSection());
			mainVBox.AddNode(CreateTextView());
			mainVBox.AddNode(CreateFooterSection());
			windowWidget.AddNode(mainVBox);
		}

		private Widget CreateHeaderSection()
		{
			var header = new Widget {
				Layout = new TableLayout {
					ColCount = 2,
					RowCount = 2,
					RowSpacing = 6,
					ColSpacing = 6,
					RowDefaults = new List<LayoutCell> {
						new LayoutCell { StretchY = 0 },
						new LayoutCell { StretchY = 0 },
					},
					ColDefaults = new List<LayoutCell> {
						new LayoutCell { StretchX = 0 },
						new LayoutCell(),
					}
				},
				LayoutCell = new LayoutCell { StretchY = 0 }
			};
			AddPicker(header, "Target platform", platformPicker = new PlatformPicker());
			AddPicker(header, "Citrus Project", projectPicker = CreateProjectPicker());
			return header;
		}

		private Widget CreateVcsSection()
		{
			updateVcs = new CheckBoxWithLabel("Update project before build");
			return updateVcs;
		}

		private static void AddPicker(Node table, string name, Node picker)
		{
			var label = new ThemedSimpleText(name) {
				VAlignment = VAlignment.Center,
				HAlignment = HAlignment.Left
			};
			label.MinHeight = Theme.Metrics.DefaultButtonSize.Y;
			table.AddNode(label);
			table.AddNode(picker);
		}

		private static FileChooser CreateProjectPicker()
		{
			var picker = new FileChooser();
			picker.FileChosenByUser += The.Workspace.Open;
			return picker;
		}

		private Widget CreateTextView()
		{
			textView = new ThemedTextView();
			textWriter = new TextViewWriter(textView);
			Console.SetOut(textWriter);
			Console.SetError(textWriter);
			var menu = new Menu();
			var shCopy = new Shortcut(Modifiers.Control, Key.C);
			var command = new Command
			{
				Shortcut = shCopy,
				Text = "Copy All",
			};
			menu.Add(command);
			textView.Updated += (dt) => {
				if (textView.Input.WasKeyPressed(Key.Mouse1)) {
					menu.Popup();
				}
				if (command.WasIssued()) {
					command.Consume();
					Clipboard.Text = textView.Text;
				}
			};

			return textView;
		}

		private ThemedDropDownList actionPicker;

		private Widget CreateFooterSection()
		{
			var container = new Widget {
				Layout = new HBoxLayout {
					Spacing = 5
				},
			};
			actionPicker = new ThemedDropDownList();

			foreach (var menuItem in The.MenuController.GetVisibleAndSortedItems()) {
				actionPicker.Items.Add(new CommonDropDownList.Item(menuItem.Label, menuItem.Action));
			}

			container.AddNode(actionPicker);
			actionPicker.Index = 0;
			var go = new ThemedButton("Go");
			go.Clicked += () => Execute((Action) actionPicker.Value);
			go.AddNode(inputLocker);
			container.AddNode(go);
			return container;
		}

		private void Execute(Action action)
		{
			windowWidget.Tasks.Add(ExecuteTask(action));
		}

		private IEnumerator<object> ExecuteTask(Action action)
		{
			var startTime = DateTime.Now;
			The.Workspace.Save();
			EnableControls(false);
			textView.Clear();
			var updateCompleted = true;
			if (DoesNeedSvnUpdate()) {
				var builder = new SolutionBuilder(The.Workspace.ActivePlatform, The.Workspace.CustomSolution);
				yield return Task.ExecuteAsync(() => updateCompleted = SafeExecute(builder.SvnUpdate));
			}
			if (updateCompleted) {
				The.Workspace?.AssetFiles?.Rescan();
				yield return Task.ExecuteAsync(() => SafeExecute(action));
				textWriter.WriteLine("Output has been copied to clipboard.");
			}
			Clipboard.Text = textView.Text;
			EnableControls(true);
			ShowTimeStatistics(startTime);
		}

		private bool SafeExecute(Action action)
		{
			try {
				action();
			}
			catch (System.Exception ex) {
				textWriter.WriteLine(ex);
				return false;
			}
			return true;
		}

		private void EnableControls(bool value)
		{
			if (value) {
				textView.Input.DerestrictScope();
			}
			else {
				textView.Input.RestrictScope();
			}
		}

		private void ShowTimeStatistics(DateTime startTime)
		{
			var endTime = DateTime.Now;
			var delta = endTime - startTime;
			Console.WriteLine("Elapsed time {0}:{1}:{2}", delta.Hours, delta.Minutes, delta.Seconds);
		}

		public override void OnWorkspaceOpened()
		{
			platformPicker.Reload();
		}

		public override void OnWorkspaceLoaded(WorkspaceConfig config)
		{
			updateVcs.CheckBox.Checked = config.UpdateBeforeBuild;
			projectPicker.ChosenFile = config.CitrusProject;
		}

		public override void ClearLog()
		{
			textView.Clear();
		}

		public override void RefreshMenu()
		{
			actionPicker.Items.Clear();
			foreach (var menuItem in The.MenuController.GetVisibleAndSortedItems()) {
				actionPicker.Items.Add(new CommonDropDownList.Item(menuItem.Label, menuItem.Action));
			}
		}

		public override bool AskConfirmation(string text)
		{
			bool? result = null;
			Application.InvokeOnMainThread(() => result = ConfirmationDialog.Show(text));
			while (result == null) {
				Thread.Sleep(1);
			}
			return result.Value;
		}

		public override bool AskChoice(string text, out bool yes)
		{
			yes = true;
			return true;
		}

		public override void ShowError(string message)
		{
			Application.InvokeOnMainThread(() => AlertDialog.Show(message));
		}

		public override Target GetActiveTarget()
		{
			return platformPicker.SelectedTarget;
		}

		public override bool DoesNeedSvnUpdate()
		{
			return updateVcs.Checked;
		}

		public override IPluginUIBuilder GetPluginUIBuilder()
		{
			return new PluginUIBuidler();
		}

		public override void CreatePluginUI(IPluginUIBuilder builder)
		{
			if (!builder.SidePanel.Enabled) {
				return;
			}
			pluginPanel = builder.SidePanel as PluginPanel;
			windowWidget.AddNode(pluginPanel);
			window.ClientSize = new Vector2(window.ClientSize.X + 150, window.ClientSize.Y);
		}

		public override void DestroyPluginUI()
		{
			windowWidget.Nodes.Remove(pluginPanel);
			if (pluginPanel != null) {
				window.ClientSize = new Vector2(window.ClientSize.X - 150, window.ClientSize.Y);
				pluginPanel = null;
			}
		}

		private class TextViewWriter : TextWriter
		{
			private readonly ThemedTextView textView;

			public TextViewWriter(ThemedTextView textView)
			{
				this.textView = textView;
			}

			public override void WriteLine(string value)
			{
				Write(value + '\n');
			}

			public override void Write(string value)
			{
				Application.InvokeOnMainThread(() => {
					textView.Append(value);
					textView.ScrollToEnd();
				});
			}

			public override Encoding Encoding { get; }
		}
	}
}