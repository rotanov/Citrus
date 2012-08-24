using System.Collections;
using System.IO;
using System.Collections.Generic;
using System;
using ProtoBuf;

namespace Lime
{
	[ProtoContract]
	public class SerializableTexture : ITexture
	{
		SerializableTextureCore core;

		public SerializableTexture()
		{
			core = TexturePool.Instance.GetSerializableTextureCore("");
		}

		public SerializableTexture(string path)
		{
			core = TexturePool.Instance.GetSerializableTextureCore(path);
		}

		public SerializableTexture(string format, params object[] args)
		{
			var path = string.Format(format, args);
			core = TexturePool.Instance.GetSerializableTextureCore(path);
		}

		public string SerializationPath {
			get {
                var path = core.Path;
                if (!string.IsNullOrEmpty(path) && path[0] == '#') {
                    return path;
                } else {
                    return Serialization.ShrinkPath(path);
                }
			}
			set {
                string path = value;
                if (!string.IsNullOrEmpty(value) && value[0] != '#') {
                    path = Serialization.ExpandPath(value);
                }
				core = TexturePool.Instance.GetSerializableTextureCore(path);
			}
		}

		public Size ImageSize {
			get {
				core.GetInstance();
				return core.ImageSize; 
			}
		}

		public Size SurfaceSize {
			get {
				return core.GetInstance().SurfaceSize;
			}
		}

		public Rectangle UVRect { 
			get {
				core.GetInstance();
				return core.UVRect;
			}
		}

		public uint GetHandle()
		{
			return core.GetInstance().GetHandle();
		}

		public void SetAsRenderTarget()
		{
			core.GetInstance().SetAsRenderTarget();
		}

		public void RestoreRenderTarget()
		{
			core.GetInstance().RestoreRenderTarget();
		}

		public bool IsTransparentPixel(int x, int y)
		{
			return core.GetInstance().IsTransparentPixel(x, y);
		}

		public override string ToString()
		{
			return core.Path;
		}

		public void Dispose()
		{
		}
	}

	class SerializableTextureCore
	{
		public readonly string Path;
		int usedAtRenderCycle = 0;
		
		ITexture instance;
		internal Rectangle UVRect;
		internal Size ImageSize;

		public SerializableTextureCore(string path)
		{
			Path = path;
		}

		~SerializableTextureCore()
		{
			Discard();
		}
		
		static PlainTexture CreateStubTexture()
		{
			var stubTexture = new PlainTexture();
			Color4[] pixels = new Color4[128 * 128];
			for (int i = 0; i < 128; i++)
				for (int j = 0; j < 128; j++)
					pixels[i * 128 + j] = (((i + (j & ~7)) & 8) == 0) ? Color4.Blue : Color4.White;
			stubTexture.LoadImage(pixels, 128, 128, true);
			return stubTexture;
		}

		/// <summary>
		/// Discards texture, frees graphics resources.
		/// </summary>
		public void Discard()
		{
			if (instance != null) {
				if (instance is IDisposable)
					(instance as IDisposable).Dispose();
				instance = null;
			}
		}

		/// <summary>
		/// Discards texture if it has not been used 
		/// for given number of game cycles.
		/// </summary>
		public void DiscardIfNotUsed(int numCycles)
		{
			if ((Renderer.RenderCycle - usedAtRenderCycle) >= numCycles)
				Discard();
		}
		
		private bool TryCreateRenderTarget(string path)
		{
			if (Path.Length > 0 && Path[0] == '#') {
				switch(Path) {
				case "#a":
				case "#b":
					instance = new RenderTexture(256, 256);
					break;
				case "#c":
					instance = new RenderTexture(512, 512);
					break;
				case "#d":
					instance = new RenderTexture(1024, 1024);
					break;
				default:
					instance = CreateStubTexture();
					break;
				}
				UVRect.A = Vector2.Zero;
				UVRect.B = Vector2.One;
				ImageSize = instance.ImageSize;
				return true;
			}
			return false;
		}
		
		private bool TryLoadTextureAtlasPart(string path)
		{
			if (AssetsBundle.Instance.FileExists(path)) {
				var texParams = TextureAtlasPart.ReadFromBundle(path);
				instance = new SerializableTexture(texParams.AtlasTexture);
				UVRect.A = (Vector2)texParams.AtlasRect.A / (Vector2)instance.SurfaceSize;
				UVRect.B = (Vector2)texParams.AtlasRect.B / (Vector2)instance.SurfaceSize;
				ImageSize = (Size)texParams.AtlasRect.Size;
				return true;
			}
			return false;
		}
		
		private bool TryLoadImage(string path)
		{
			if (AssetsBundle.Instance.FileExists(path)) {
				instance = new PlainTexture();
				(instance as PlainTexture).LoadImage(path);
				UVRect.A = Vector2.Zero;
				UVRect.B = (Vector2)instance.ImageSize / (Vector2)instance.SurfaceSize;
				ImageSize = instance.ImageSize;
				return true;
			}
			Console.WriteLine("Missing texture '{0}'", path);
			return false;
		}

		public ITexture GetInstance()
		{
			if (instance == null) {
				bool loaded = !string.IsNullOrEmpty(Path) && (TryCreateRenderTarget(Path) ||
					TryLoadTextureAtlasPart(Path + ".atlasPart") ||
#if iOS
					TryLoadImage(Path + ".pvr")
#else
					TryLoadImage(Path + ".dds")
#endif
				);
				if (!loaded) {
					instance = CreateStubTexture();
					UVRect = instance.UVRect;
					ImageSize = instance.ImageSize;
				}
			}
			usedAtRenderCycle = Renderer.RenderCycle;
			return instance;
		}
	}

	public sealed class TexturePool
	{
		Dictionary<string, WeakReference> items;
		static readonly TexturePool instance = new TexturePool();

		public static TexturePool Instance { get { return instance; } }

		private TexturePool()
		{
			items = new Dictionary<string, WeakReference>();
		}

		/// <summary>
		/// Discards textures wich have not been used 
		/// for given number of render cycles.
		/// </summary>
		public void DiscardUnusedTextures(int numCycles)
		{
			foreach (WeakReference r in items.Values) {
				if (r.IsAlive)
					(r.Target as SerializableTextureCore).DiscardIfNotUsed(numCycles);
			}
			PlainTexture.DeleteScheduledTextures();
		}

		public void DiscardAllTextures()
		{
			foreach (WeakReference r in items.Values) {
				if (r.IsAlive) {
					(r.Target as SerializableTextureCore).Discard();
				}
			}
			PlainTexture.DeleteScheduledTextures();
		}

		internal SerializableTextureCore GetSerializableTextureCore(string path)
		{
			WeakReference r;
			if (!items.TryGetValue(path, out r) || !r.IsAlive) {
				r = new WeakReference(new SerializableTextureCore(path));
				items[path] = r;
			}
			return r.Target as SerializableTextureCore;
		}
	}
}