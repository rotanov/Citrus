namespace Lime
{
	internal class PostProcessingActionHSL : PostProcessingAction
	{
		public override bool Enabled => RenderObject.HSLEnabled;
		public override PostProcessingAction.Buffer TextureBuffer => RenderObject.HSLBuffer;

		public override void Do()
		{
			if (RenderObject.HSLBuffer.EqualRenderParameters(RenderObject)) {
				RenderObject.ProcessedTexture = RenderObject.HSLBuffer.Texture;
				RenderObject.CurrentBufferSize = (Vector2)RenderObject.HSLBuffer.Size;
				RenderObject.ProcessedUV1 = (Vector2)RenderObject.ViewportSize / RenderObject.CurrentBufferSize;
				return;
			}

			RenderObject.PrepareOffscreenRendering(RenderObject.Size);
			RenderObject.HSLMaterial.HSL = RenderObject.HSL;
			RenderObject.RenderToTexture(RenderObject.HSLBuffer.Texture, RenderObject.ProcessedTexture, RenderObject.HSLMaterial, Color4.White, Color4.Zero);

			RenderObject.HSLBuffer.SetRenderParameters(RenderObject);
			RenderObject.MarkBuffersAsDirty = true;
			RenderObject.ProcessedTexture = RenderObject.HSLBuffer.Texture;
			RenderObject.CurrentBufferSize = (Vector2)RenderObject.HSLBuffer.Size;
			RenderObject.ProcessedUV1 = (Vector2)RenderObject.ViewportSize / RenderObject.CurrentBufferSize;
		}

		internal new class Buffer : PostProcessingAction.Buffer
		{
			private Vector3 hsl = new Vector3(float.NaN, float.NaN, float.NaN);

			public Buffer(Size size) : base(size) { }

			public bool EqualRenderParameters(PostProcessingRenderObject ro) => !IsDirty && hsl == ro.HSL;

			public void SetRenderParameters(PostProcessingRenderObject ro)
			{
				IsDirty = false;
				hsl = ro.HSL;
			}
		}
	}
}
