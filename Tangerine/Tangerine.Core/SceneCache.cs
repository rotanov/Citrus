using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Lime;
using Yuzu = Lime.Yuzu;

namespace Tangerine.Core
{
	public class SceneCache
	{
		private class CacheEntry
		{
			public Node Node
			{
				get
				{
					var r = nodeProvider?.Invoke() ?? node;
					if (r != null && DoNeedReloadExternalScenes) {
						DoNeedReloadExternalScenes = false;
						r.LoadExternalScenes(TangerineYuzu.Instance.Value);
					}
					return r;
				}
			}
			private Func<Node> nodeProvider;
			private Node node;
			public HashSet<string> Dependencies
			{
				get
				{
					if (!areDependenciesValid) {
						RefreshDependencies();
					}
					return dependencies;
				}
			}

			private readonly HashSet<string> dependencies = new HashSet<string>();
			bool areDependenciesValid = false;
			public bool DoNeedReloadExternalScenes { get; set; }
			public void SetNodeProvider(Func<Node> nodeProvider)
			{
				areDependenciesValid = false;
				this.nodeProvider = nodeProvider;
			}
			public void SetNode(Node node)
			{
				areDependenciesValid = nodeProvider != null;
				this.node = node;
			}

			private void RefreshDependencies()
			{
				var sw = System.Diagnostics.Stopwatch.StartNew();
				dependencies.Clear();
				if (Node == null) {
					return;
				}
				foreach (var descendant in Node.Descendants) {
					string contentsPath = descendant.ContentsPath;
					if (!contentsPath.IsNullOrWhiteSpace() && Node.ResolveScenePath(contentsPath) != null) {
						dependencies.Add(contentsPath);
					}
				}
				areDependenciesValid = true;
				sw.Stop();
				Console.WriteLine($"RefreshDependencies took {sw.ElapsedMilliseconds} ms");
			}
		}

		public SceneCache()
		{
			Node.SceneLoading = new ThreadLocal<Node.SceneLoadingDelegate>(() => SceneCache_SceneLoading);
			Node.SceneLoaded = new ThreadLocal<Node.SceneLoadedDelegate>(() => SceneCache_SceneLoaded);
		}

		private readonly Dictionary<string, CacheEntry> contentPathToCacheEntry = new Dictionary<string, CacheEntry>();

		private bool SceneCache_SceneLoading(string path, ref Node instance, bool external)
		{
			Console.WriteLine($"Loading: {path}, external: {external}");
			if (!external) {
				return false;
			}
			if (!contentPathToCacheEntry.TryGetValue(path, out var t)) {
				contentPathToCacheEntry.Add(path, new CacheEntry());
				return false;
			} else if (t.Node == null) {
				return false;
			}
			instance = t.Node.Clone();
			return true;
		}

		private void SceneCache_SceneLoaded(string path, Node instance, bool external)
		{
			Console.WriteLine($"Loaded: {path}, external: {external}");
			if (!external) {
				return;
			}
			if (!contentPathToCacheEntry.TryGetValue(path, out var t)) {
				throw new InvalidOperationException();
			}
			if (t.Node == null) {
				t.SetNode(instance.Clone());
			} else {
				return;
			}
		}

		public void InvalidateEntryFromFilesystem(string path)
		{
			var t = GetCacheEntrySafe(path);
			t.SetNode(null);
			MarkDependentsForReload(path);
		}

		public void InvalidateEntryFromOpenedDocumentChanged(string path, Func<Node> nodeProviderFunc)
		{
			var t = GetCacheEntrySafe(path);
			t.SetNodeProvider(nodeProviderFunc);
			MarkDependentsForReload(path);
		}

		private CacheEntry GetCacheEntrySafe(string path)
		{
			if (!contentPathToCacheEntry.TryGetValue(path, out CacheEntry t)) {
				contentPathToCacheEntry.Add(path, t = new CacheEntry());
			}
			return t;
		}

		private void MarkDependentsForReload(string path)
		{
			var q = new Queue<string>();
			q.Enqueue(path);
			while (q.Count != 0) {
				var nextPath = q.Dequeue();
				foreach (var kv in contentPathToCacheEntry) {
					if (kv.Key == path) {
						continue;
					}
					if (kv.Value.Dependencies.Contains(nextPath)) {
						kv.Value.DoNeedReloadExternalScenes = true;
						q.Enqueue(kv.Key);
					}
				}
			}
		}

		public void Clear(string docPath)
		{
			if (contentPathToCacheEntry.TryGetValue(docPath, out CacheEntry t)) {
				contentPathToCacheEntry.Remove(docPath);
				foreach (var d in t.Dependencies) {
					Clear(d);
				}
			}
		}
	}
}
