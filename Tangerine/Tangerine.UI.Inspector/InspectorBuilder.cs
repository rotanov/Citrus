﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Yuzu;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI.Inspector
{
	class InspectorBuilder
	{
		Inspector Inspector => Inspector.Instance;

		public void Build(IEnumerable<object> objects)
		{
			var content = Inspector.ContentWidget;
			if (Widget.Focused != null && Widget.Focused.DescendantOf(content)) {
				content.SetFocus();
			}
			content.Nodes.Clear();
			Inspector.Editors.Clear();
			foreach (var t in GetTypes(objects)) {
				var o = objects.Where(i => t.IsInstanceOfType(i)).ToList();
				PopulateContentForType(t, o);
			}
		}

		IEnumerable<Type> GetTypes(IEnumerable<object> objects)
		{
			var types = new List<Type>();
			foreach (var o in objects) {
				var inheritanceList = new List<Type>();
				for (var t = o.GetType(); t != typeof(object); t = t.BaseType) {
					inheritanceList.Add(t);
				}
				inheritanceList.Reverse();
				foreach (var t in inheritanceList) {
					if (!types.Contains(t)) {
						types.Add(t);
					}
				}
			}
			return types;
		}

		void PopulateContentForType(Type type, List<object> objects)
		{
			var categoryLabelAdded = false;
			foreach (var property in type.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public)) {
				if (property.Name == "Item") {
					// WTF, Bug in Mono?
					continue;
				}
				var yuzuField = PropertyAttributes<YuzuField>.Get(type, property.Name);
				var tang = PropertyAttributes<TangerineKeyframeColorAttribute>.Get(type, property.Name);
				var tangIgnore = PropertyAttributes<TangerineIgnorePropertyAttribute>.Get(type, property.Name);
				if (yuzuField == null && tang == null || tangIgnore != null)
					continue;
				if (!categoryLabelAdded) {
					categoryLabelAdded = true;
					var text = type.Name;
					if (text == "Node" && objects.Count == 1) {
						text += $" of type '{objects[0].GetType().Name}'";
					}
					var label = new Widget {
						LayoutCell = new LayoutCell { StretchY = 0 },
						Layout = new StackLayout(),
						MinHeight = DesktopTheme.Metrics.DefaultButtonSize.Y,
						Nodes = {
							new SimpleText {
								Text = text,
								Padding = new Thickness(4, 0),
								VAlignment = VAlignment.Center,
								AutoSizeConstraints = false,
							}
						}
					};
					label.CompoundPresenter.Add(new WidgetFlatFillPresenter(ColorTheme.Current.Inspector.CategoryLabelBackground));
					Inspector.ContentWidget.AddNode(label);
				}
				var context = new PropertyEditorContext(Inspector.ContentWidget, objects, type, property.Name) {
					NumericEditBoxFactory = () => new TransactionalNumericEditBox(),
					PropertySetter = Core.Operations.SetAnimableProperty.Perform
				};
				foreach (var i in Inspector.PropertyEditorRegistry) {
					if (i.Condition(context)) {
						var propertyEditor = i.Builder(context);
						if (propertyEditor != null) {
							DecoratePropertyEditor(propertyEditor);
							Inspector.Editors.Add(propertyEditor);
						}
						break;
					}
				}
			}
		}

		private void DecoratePropertyEditor(IPropertyEditor editor)
		{
			var ctr = editor.ContainerWidget;
			if (PropertyAttributes<TangerineStaticPropertyAttribute>.Get(editor.Context.PropertyInfo) == null) {
				var keyFunctionButton = new KeyFunctionButton {
					LayoutCell = new LayoutCell(Alignment.LeftCenter, stretchX: 0),
				};
				var keyframeButton = new KeyframeButton {
					LayoutCell = new LayoutCell(Alignment.LeftCenter, stretchX: 0),
					KeyColor = KeyframePalette.Colors[editor.Context.TangerineAttribute.ColorIndex],
				};
				keyFunctionButton.Clicked += editor.SetFocus;
				keyframeButton.Clicked += editor.SetFocus;
				ctr.Nodes.Insert(1, keyFunctionButton);
				ctr.Nodes.Insert(2, keyframeButton);
				ctr.Nodes.Insert(3, new HSpacer(4));
				ctr.Tasks.Add(new KeyframeButtonBinding(editor.Context, keyframeButton));
				ctr.Tasks.Add(new KeyFunctionButtonBinding(editor.Context, keyFunctionButton));
			} else {
				ctr.Nodes.Insert(1, new HSpacer(41));
			}
		}

		class TransactionalNumericEditBox : NumericEditBox
		{
			public TransactionalNumericEditBox()
			{
				Theme.Current.Apply(this, typeof(NumericEditBox));
				BeginSpin += () => Document.Current.History.BeginTransaction();
				EndSpin += () => Document.Current.History.EndTransaction();
			}
		}
	}
}