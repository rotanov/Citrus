using System;
using System.Collections.Generic;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI.SceneView
{
	public class CreateNodeProcessor : ITaskProvider
	{
		public IEnumerator<object> Task()
		{
			while (true) {
				Type type;
				ICommand command;
				if (CreateNodeRequestComponent.Consume<Node>(SceneView.Instance.Components, out type, out command)) {
					try {
						Document.Current.History.DoTransaction(() => {
							Core.Operations.CreateNode.Perform(type);
						});
					} catch (InvalidOperationException e) {
						AlertDialog.Show(e.Message);
					}
				}
				yield return null;
			}
		}
	}
}
