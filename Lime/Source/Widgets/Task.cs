﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lime
{
	using EnumType = IEnumerator<object>;

	public class Task : IDisposable
	{
		public abstract class WaitPredicate
		{
			public float TotalTime;
			public abstract bool Evaluate();
		}

		public object Tag { get; set; }
		public bool Completed { get { return stack.Count == 0; } }
		private float waitTime;
		private WaitPredicate waitPredicate;
		private Stack<EnumType> stack = new Stack<EnumType>();

		public Task(EnumType e, object tag = null)
		{
			Tag = tag;
			stack.Push(e);
		}

		public void Advance(float delta)
		{
			if (waitTime > 0) {
				waitTime -= delta;
				return;
			}
			if (waitPredicate != null) {
				waitPredicate.TotalTime += delta;
				if (waitPredicate.Evaluate()) {
					return;
				}
				waitPredicate = null;
			}
			var e = stack.Peek();
			if (e.MoveNext()) {
				HandleYieldedResult(e.Current);
			} else if (!Completed) {
				stack.Pop();
			}
		}

		public void Dispose()
		{
			while (stack.Count > 0) {
				var e = stack.Pop();
				e.Dispose();
			}
			waitPredicate = null;
		}

		private void HandleYieldedResult(object result)
		{
			if (result is int) {
				waitTime = (int)result;
			} else if (result is float) {
				waitTime = (float)result;
			} else if (result is IEnumerator<object>) {
				stack.Push(result as IEnumerator<object>);
				Advance(0);
			} else if (result is WaitPredicate) {
				waitPredicate = result as WaitPredicate;
			} else if (result is Lime.Node) {
				waitPredicate = WaitForAnimation(result as Lime.Node);
			} else if (result is IEnumerable<object>) {
				throw new Lime.Exception("Use IEnumerator<object> instead of IEnumerable<object> for " + result);
			} else {
				throw new Lime.Exception("Invalid object yielded " + result);
			}
		}

		public static WaitPredicate WaitWhile(Func<bool> predicate)
		{
			return new BooleanWaitPredicate() { Preducate = predicate };
		}

		public static WaitPredicate WaitWhile(Func<float, bool> timePredicate)
		{
			return new TimeWaitPredicate() { Preducate = timePredicate };
		}
		
		public static WaitPredicate WaitForAnimation(Lime.Node node)
		{
			return new AnimationWaitPredicate() { Node = node };
		}

		private class AnimationWaitPredicate : WaitPredicate
		{
			public Node Node;

			public override bool Evaluate() { return Node.IsRunning; }
		}

		private class BooleanWaitPredicate : WaitPredicate
		{
			public Func<bool> Preducate;

			public override bool Evaluate() { return Preducate(); }
		}

		private class TimeWaitPredicate : WaitPredicate
		{
			public Func<float, bool> Preducate;

			public override bool Evaluate() { return Preducate(TotalTime); }
		}

		public static IEnumerator<object> ExecuteAsync(Action action)
		{
			var t = new System.Threading.Tasks.Task(action);
			t.Start();
			while (!t.IsCompleted && !t.IsCanceled && !t.IsFaulted) {
				yield return 0;
			}
		}
	}
}
