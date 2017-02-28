﻿using System.Collections.Generic;
using System.Linq;

namespace EmptyProject.Dialogs
{
	public class DialogContext
	{
		public static DialogContext Instance { get; } = new DialogContext();

		public List<Dialog> ActiveDialogs { get; } = new List<Dialog>();
		public Dialog Top => ActiveDialogs.FirstOrDefault();

		public void Open<T>() where T : Dialog, new()
		{
			Open(new T());
		}

		public void Open<T>(T dialog) where T : Dialog
		{
			ActiveDialogs.Add(dialog);
		}
	}
}