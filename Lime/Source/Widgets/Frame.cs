using System;
using Lime;
using ProtoBuf;
using System.IO;
using System.Collections.Generic;

namespace Lime
{
	[ProtoContract]
	public enum RenderTarget
	{
		[ProtoEnum]
		None,
		[ProtoEnum]
		A,
		[ProtoEnum]
		B,
		[ProtoEnum]
		C,
		[ProtoEnum]
		D,
	}
	
	public delegate void UpdateDelegate(float delta);

	public class KeyEventArgs : EventArgs 
	{
		public bool Consumed;
	}

	[ProtoContract]
	public class Frame : Widget, IImageCombinerArg
	{
		RenderTarget renderTarget;
		ITexture renderTexture;

		public UpdateDelegate BeforeUpdate;
		public UpdateDelegate AfterUpdate;
		public UpdateDelegate BeforeLateUpdate;
		public UpdateDelegate AfterLateUpdate;
		public Event OnRender;

		[ProtoMember(1)]
		public RenderTarget RenderTarget {
			get { return renderTarget; }
			set {
				renderTarget = value;
				renderedToTexture = value != RenderTarget.None;
				switch(value) {
				case RenderTarget.A:
					renderTexture = new SerializableTexture("#a");
					break;
				case RenderTarget.B:
					renderTexture = new SerializableTexture("#b");
					break;
				case RenderTarget.C:
					renderTexture = new SerializableTexture("#c");
					break;
				case RenderTarget.D:
					renderTexture = new SerializableTexture("#d");
					break;
				default:
					renderTexture = null;
					break;
				}
			}
		}

		public float AnimationSpeed = 1;

		// In dialog mode frame acts like a modal dialog, all controls behind the dialog are frozen.
		// If dialog is being shown or hidden then all controls on dialog are frozen either.
		public bool DialogMode;

		void IImageCombinerArg.BypassRendering() {}

		ITexture IImageCombinerArg.GetTexture()
		{
			return renderTexture;
		}

		void UpdateForDialogMode(int delta)
		{
			if (globallyVisible && Input.MouseVisible) {
				if (RootFrame.Instance.ActiveWidget != null && !RootFrame.Instance.ActiveWidget.ChildOf(this)) {
					// Discard active widget if it's not a child of the topmost dialog.
					RootFrame.Instance.ActiveWidget = null;
				}
			}
			if (globallyVisible && RootFrame.Instance.ActiveTextWidget != null && !RootFrame.Instance.ActiveTextWidget.ChildOf(this)) {
				// Discard active text widget if it's not a child of the topmost dialog.
				RootFrame.Instance.ActiveTextWidget = null;
			}
			if (!Running) {
				base.Update(delta);
			}
			if (globallyVisible) {
				// Cosume all input events and drive mouse out of the screen.
				Input.ConsumeAllKeyEvents(true);
				Input.MouseVisible = false;
				Input.TextInput = null;
			}
			if (Running) {
				base.Update(delta);
			}
		}

		public override void LateUpdate(int delta)
		{
			if (AnimationSpeed != 1) {
				delta = MultiplyDeltaByAnimationSpeed(delta);
			}
			if (BeforeLateUpdate != null) {
				BeforeLateUpdate(delta * 0.001f);
			}
			while (delta > MaxTimeDelta) {
				base.LateUpdate(MaxTimeDelta);
				delta -= MaxTimeDelta;
			}
			base.LateUpdate(delta);
			if (AfterLateUpdate != null) {
				AfterLateUpdate(delta * 0.001f);
			}
		}

		void UpdateHelper(int delta)
		{
			if (DialogMode) {
				UpdateForDialogMode(delta);
			} else {
				base.Update(delta);
			}
		}

		public override void Update(int delta)
		{
			if (AnimationSpeed != 1) {
				delta = MultiplyDeltaByAnimationSpeed(delta);
			}
			if (BeforeUpdate != null) {
				BeforeUpdate(delta * 0.001f);
			}
			while (delta > MaxTimeDelta) {
				UpdateHelper(MaxTimeDelta);
				delta -= MaxTimeDelta;
			}
			UpdateHelper(delta);
			if (AfterUpdate != null) {
				AfterUpdate(delta * 0.001f);
			}
		}

		int MultiplyDeltaByAnimationSpeed(int delta)
		{
			delta = (int)(delta * AnimationSpeed);
			if (delta < 0) {
				throw new System.ArgumentOutOfRangeException("delta");
			}
			return delta;
		}

		public override void Render()
		{
			if (renderTexture != null) {
				RenderToTexture(renderTexture);
			}
			if (OnRender != null) {
				Renderer.Transform1 = GlobalMatrix;
				OnRender();
			}
		}

		public override void AddToRenderChain(RenderChain chain)
		{
			if (globallyVisible) {
				if (renderTexture != null)
					chain.Add(this);
				else
					base.AddToRenderChain(chain);
			}
		}

		private static HashSet<string> processingFiles = new HashSet<string>();

		public static Frame Create(string path)
		{
			path = Path.ChangeExtension(path, "scene");
			if (processingFiles.Contains(path))
				throw new Lime.Exception("Cyclic dependency of scenes has detected: {0}", path);
			Frame frame;
			processingFiles.Add(path);
			try {
				using (Stream stream = AssetsBundle.Instance.OpenFileLocalized(path)) {
					frame = Serialization.ReadObject<Frame>(path, stream);
				}
				frame.LoadContent();
				frame.Tag = path;
			} finally {
				processingFiles.Remove(path);
			}
			return frame;
		}

		public void LoadContent()
		{
			if (!string.IsNullOrEmpty(ContentsPath))
				LoadContentHelper();
			else {
				foreach (Node node in Nodes.AsArray) {
					if (node is Frame) {
						(node as Frame).LoadContent();
					}
				}
			}
		}

		void LoadContentHelper()
		{
			Nodes.Clear();
			Markers.Clear();
			if (AssetsBundle.Instance.FileExists(ContentsPath)) {
				Frame content = Frame.Create(ContentsPath);
				if (content.Widget != null && Widget != null) {
					content.Update(0);
					content.Widget.Size = Widget.Size;
					content.Update(0);
				}
				foreach (Marker marker in content.Markers)
					Markers.Add(marker);
				foreach (Node node in content.Nodes.AsArray)
					Nodes.Add(node);
			}
		}

		public static Frame CreateSubframe(string path)
		{
			var frame = Create(path).Nodes[0] as Frame;
			frame.Parent = null;
			frame.Tag = path;
			return frame;
		}

		public static Frame CreateAndRun(Node parent, string path, string marker = null)
		{
			Frame frame = Create(path);
			frame.Tag = path;
			if (marker == null) {
				frame.Running = true;
			} else {
				frame.RunAnimation(marker);
			}
			if (parent != null) {
				parent.Nodes.Insert(0, frame);
			}
			return frame;
		}

		public static Frame CreateSubframeAndRun(Node parent, string path, string marker = null)
		{
			Frame frame = Frame.Create(path).Nodes[0] as Frame;
			frame.Parent = null;
			frame.Tag = path;
			if (marker == null) {
				frame.Running = true;
			} else {
				frame.RunAnimation(marker);
			}
			if (parent != null) {
				parent.Nodes.Insert(0, frame);
			}
			return frame;
		}
	}
}
