#if !ANDROID && !iOS && !WIN
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace Lime
{
	public class VideoDecoder: IDisposable
	{
		public int Width => 0;
		public int Height => 0;
		public bool HasNewTexture = false;
		public bool Looped = false;
		public ITexture Texture => null;
		public Action OnStart;
		public VideoPlayerStatus Status = VideoPlayerStatus.Playing;
		public float CurrentPosition { get; set; }
		public float Duration { get; }
		public bool MuteAudio = false;

		public VideoDecoder(string path)
		{
		}

		public async System.Threading.Tasks.Task Start()
		{
		}

		public void Stop()
		{
		}

		public void Pause()
		{
		}

		public void Update(float delta)
		{
		}

		public void UpdateTexture()
		{
		}

		public void Dispose()
		{
		}
	}
}
#endif
