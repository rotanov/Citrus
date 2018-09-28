using Yuzu;

namespace Lime
{
	// TODO: Attribute to exclude components with custom presenters and/or custom render chain builders
	[TangerineRegisterComponent]
	[AllowedComponentOwnerTypes(typeof(Widget))]
	public class PostProcessingComponent : NodeBehavior
	{
		private const string GroupHSL = "1. HSL";
		private const string GroupBlur = "2. Blur effect";
		private const string GroupBloom = "3. Bloom effect";
		private const string GroupNoise = "4. Noise";
		private const string GroupVignette = "5. Vignette";
		private const string GroupOverallImpact = "6. Overall impact";
		private const string GroupSourceTexture = "7. Source texture";
		private const string GroupDebugView = "8. Debug view";
		private const int MinimumTextureSize = 32;
		private const int MaximumTextureSize = 2048;
		private const float MinimumTextureScaling = 0.01f;
		private const float MaximumTextureScaling = 1f;
		private const float MaximumHue = 1f;
		private const float MaximumSaturation = 1f;
		private const float MaximumLightness = 1f;
		private const float MaximumBlurRadius = 10f;
		private const float MaximumGammaCorrection = 10f;

		internal HSLMaterial HSLMaterial { get; private set; } = new HSLMaterial();
		internal BlurMaterial BlurMaterial { get; private set; } = new BlurMaterial();
		internal BloomMaterial BloomMaterial { get; private set; } = new BloomMaterial();
		internal SoftLightMaterial SoftLightMaterial { get; private set; } = new SoftLightMaterial();
		internal VignetteMaterial VignetteMaterial { get; private set; } = new VignetteMaterial();

		// TODO: Solve promblem of storing and restoring savedPresenter&savedRenderChainBuilder
		private PostProcessingPresenter presenter = new PostProcessingPresenter();
		private PostProcessingRenderChainBuilder renderChainBuilder = new PostProcessingRenderChainBuilder();
		private Vector3 hsl = new Vector3(0, 1, 1);
		private float blurRadius = 1f;
		private float blurTextureScaling = 1f;
		private float blurAlphaCorrection = 1f;
		private float bloomStrength = 1f;
		private float bloomBrightThreshold = 1f;
		private Vector3 bloomGammaCorrection = Vector3.One;
		private float bloomTextureScaling = 0.5f;
		private float noiseStrength = 1f;
		private ITexture noiseTexture;
		private float vignetteRadius = 0.5f;
		private float vignetteSoftness = 0.05f;
		private Size textureSizeLimit = new Size(256, 256);
		private bool refreshSourceTexture = true;
		private int refreshSourceRate;
		private float refreshSourceDelta;

		[YuzuMember]
		[TangerineGroup(GroupHSL)]
		public bool HSLEnabled { get; set; }

		[YuzuMember]
		[TangerineGroup(GroupHSL)]
		public float Hue
		{
			get => hsl.X * 360f;
			set => hsl.X = Mathf.Clamp(value / 360f, 0f, MaximumHue);
		}

		[YuzuMember]
		[TangerineGroup(GroupHSL)]
		public float Saturation
		{
			get => hsl.Y * 100f;
			set => hsl.Y = Mathf.Clamp(value * 0.01f, 0f, MaximumSaturation);
		}

		[YuzuMember]
		[TangerineGroup(GroupHSL)]
		public float Lightness
		{
			get => hsl.Z * 100f;
			set => hsl.Z = Mathf.Clamp(value * 0.01f, 0f, MaximumLightness);
		}

		public Vector3 HSL => hsl;

		[YuzuMember]
		[TangerineGroup(GroupBlur)]
		public bool BlurEnabled { get; set; }

		[YuzuMember]
		[TangerineGroup(GroupBlur)]
		public float BlurRadius
		{
			get => blurRadius;
			set => blurRadius = Mathf.Clamp(value, 0f, MaximumBlurRadius);
		}

		[YuzuMember]
		[TangerineGroup(GroupBlur)]
		public float BlurTextureScaling
		{
			get => blurTextureScaling * 100f;
			set => blurTextureScaling = Mathf.Clamp(value * 0.01f, MinimumTextureScaling, MaximumTextureScaling);
		}

		[YuzuMember]
		[TangerineGroup(GroupBlur)]
		public float BlurAlphaCorrection
		{
			get => blurAlphaCorrection;
			set => blurAlphaCorrection = Mathf.Clamp(value, 1f, MaximumGammaCorrection);
		}

		[YuzuMember]
		[TangerineGroup(GroupBlur)]
		public Color4 BlurBackgroundColor { get; set; } = new Color4(127, 127, 127, 0);

		[YuzuMember]
		[TangerineGroup(GroupBloom)]
		public bool BloomEnabled { get; set; }

		[YuzuMember]
		[TangerineGroup(GroupBloom)]
		public float BloomStrength
		{
			get => bloomStrength;
			set => bloomStrength = Mathf.Clamp(value, 0f, MaximumBlurRadius);
		}

		[YuzuMember]
		[TangerineGroup(GroupBloom)]
		public float BloomBrightThreshold
		{
			get => bloomBrightThreshold * 100f;
			set => bloomBrightThreshold = Mathf.Clamp(value * 0.01f, 0f, 1f);
		}

		[YuzuMember]
		[TangerineGroup(GroupBloom)]
		public Vector3 BloomGammaCorrection
		{
			get => bloomGammaCorrection;
			set => bloomGammaCorrection = new Vector3(
				Mathf.Clamp(value.X, 0f, MaximumGammaCorrection),
				Mathf.Clamp(value.Y, 0f, MaximumGammaCorrection),
				Mathf.Clamp(value.Z, 0f, MaximumGammaCorrection)
			);
		}

		[YuzuMember]
		[TangerineGroup(GroupBloom)]
		public float BloomTextureScaling
		{
			get => bloomTextureScaling * 100f;
			set => bloomTextureScaling = Mathf.Clamp(value * 0.01f, MinimumTextureScaling, MaximumTextureScaling);
		}

		[YuzuMember]
		[TangerineGroup(GroupBloom)]
		public Color4 BloomColor { get; set; } = Color4.White;

		[YuzuMember]
		[TangerineGroup(GroupNoise)]
		public bool NoiseEnabled { get; set; }

		[YuzuMember]
		[TangerineGroup(GroupNoise)]
		public float NoiseStrength
		{
			get => noiseStrength * 100f;
			set => noiseStrength = Mathf.Clamp(value * 0.01f, 0.01f, 1f);
		}

		[YuzuMember]
		[TangerineGroup(GroupNoise)]
		[YuzuSerializeIf(nameof(IsNotRenderTexture))]
		public ITexture NoiseTexture
		{
			get => noiseTexture;
			set {
				if (noiseTexture != value) {
					noiseTexture = value;
					Window.Current?.Invalidate();
				}
			}
		}

		[YuzuMember]
		[TangerineGroup(GroupVignette)]
		public bool VignetteEnabled { get; set; }

		[YuzuMember]
		[TangerineGroup(GroupVignette)]
		public float VignetteRadius
		{
			get => vignetteRadius * 100f;
			set => vignetteRadius = Mathf.Clamp(value * 0.01f, 0f, 1f);
		}

		[YuzuMember]
		[TangerineGroup(GroupVignette)]
		public float VignetteSoftness
		{
			get => vignetteSoftness * 100f;
			set => vignetteSoftness = Mathf.Clamp(value * 0.01f, 0f, 1f);
		}

		[YuzuMember]
		[TangerineGroup(GroupVignette)]
		public Vector2 VignetteScale { get; set; } = Vector2.One;

		[YuzuMember]
		[TangerineGroup(GroupVignette)]
		public Vector2 VignettePivot { get; set; } = Vector2.Half;

		[YuzuMember]
		[TangerineGroup(GroupVignette)]
		public Color4 VignetteColor { get; set; } = Color4.Black;

		[YuzuMember]
		[TangerineGroup(GroupOverallImpact)]
		public bool OverallImpactEnabled { get; set; }

		[YuzuMember]
		[TangerineGroup(GroupOverallImpact)]
		public Color4 OverallImpactColor { get; set; } = Color4.White;

		[TangerineInspect]
		[TangerineGroup(GroupSourceTexture)]
		public int SourceTextureWidth
		{
			get => textureSizeLimit.Width;
			set {
				textureSizeLimit.Width = Mathf.Clamp(value, MinimumTextureSize, MaximumTextureSize);
				RequiredRefreshSource = true;
			}
		}

		[TangerineInspect]
		[TangerineGroup(GroupSourceTexture)]
		public int SourceTextureHeight
		{
			get => textureSizeLimit.Height;
			set {
				textureSizeLimit.Height = Mathf.Clamp(value, MinimumTextureSize, MaximumTextureSize);
				RequiredRefreshSource = true;
			}
		}

		[YuzuMember]
		[TangerineIgnore]
		public Size TextureSizeLimit
		{
			get => textureSizeLimit;
			set {
				SourceTextureWidth = value.Width;
				SourceTextureHeight = value.Height;
			}
		}

		[YuzuMember]
		[TangerineGroup(GroupSourceTexture)]
		public bool RefreshSourceTexture
		{
			get => refreshSourceTexture;
			set {
				if (refreshSourceTexture != value) {
					refreshSourceTexture = value;
					if (value) {
						RequiredRefreshSource = true;
					}
				}
			}
		}

		[YuzuMember]
		[TangerineGroup(GroupSourceTexture)]
		public int RefreshSourceRate
		{
			get => refreshSourceRate;
			set {
				value = Mathf.Clamp(value, 0, int.MaxValue);
				if (refreshSourceRate != value) {
					refreshSourceRate = value;
					if (refreshSourceTexture) {
						RequiredRefreshSource = true;
					}
				}
			}
		}

		public bool RequiredRefreshSource { get; set; } = true;

		[TangerineGroup(GroupDebugView)]
		[TangerineInspect]
		public PostProcessingPresenter.DebugViewMode DebugViewMode { get; set; } = PostProcessingPresenter.DebugViewMode.None;

		public bool IsNotRenderTexture() => !(noiseTexture is RenderTexture);

		public override void Update(float delta)
		{
			refreshSourceDelta += delta;
			if (RefreshSourceTexture) {
				var d = 1f / refreshSourceRate;
				if (RefreshSourceRate == 0) {
					RequiredRefreshSource = true;
				} else if (refreshSourceDelta >= d) {
					RequiredRefreshSource = true;
					refreshSourceDelta %= d;
				}
			}
		}

		public void GetOwnerRenderObjects(RenderChain renderChain, RenderObjectList roObjects)
		{
			Owner.RenderChainBuilder = Owner;
			Owner.Presenter = DefaultPresenter.Instance;
			Owner.AddToRenderChain(renderChain);
			renderChain.GetRenderObjects(roObjects);
			Owner.RenderChainBuilder = renderChainBuilder;
			Owner.Presenter = presenter;
		}

		protected override void OnOwnerChanged(Node oldOwner)
		{
			base.OnOwnerChanged(oldOwner);
			if (oldOwner != null) {
				oldOwner.Presenter = DefaultPresenter.Instance;
				oldOwner.RenderChainBuilder = oldOwner;
				renderChainBuilder.Owner = null;
			}
			if (Owner != null) {
				Owner.Presenter = presenter;
				Owner.RenderChainBuilder = renderChainBuilder;
				renderChainBuilder.Owner = Owner.AsWidget;
			}
		}

		public override NodeComponent Clone()
		{
			var clone = (PostProcessingComponent)base.Clone();
			clone.presenter = (PostProcessingPresenter)presenter.Clone();
			clone.renderChainBuilder = (PostProcessingRenderChainBuilder)renderChainBuilder.Clone(null);
			clone.HSLMaterial = (HSLMaterial)HSLMaterial.Clone();
			clone.BlurMaterial = (BlurMaterial)BlurMaterial.Clone();
			clone.BloomMaterial = (BloomMaterial)BloomMaterial.Clone();
			clone.SoftLightMaterial = (SoftLightMaterial)SoftLightMaterial.Clone();
			clone.VignetteMaterial = (VignetteMaterial)VignetteMaterial.Clone();
			return clone;
		}
	}
}
