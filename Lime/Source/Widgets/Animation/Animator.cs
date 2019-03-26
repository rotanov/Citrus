using System;
using System.Collections;
using System.Collections.Generic;
using Yuzu;
using System.Collections.Concurrent;

namespace Lime
{
	public interface IAnimator : IDisposable
	{
		IAnimationHost Owner { get; set; }
		IAnimable Animable { get; }

		IAnimator Clone();

		bool IsTriggerable { get; set; }

		string TargetPropertyPath { get; set; }
		int TargetPropertyPathUID { get; }

		string AnimationId { get; set; }

		bool Enabled { get; set; }

		int Duration { get; }

		object CalcValue(double time);
		void Apply();
		void CalcAndApply(double time);
		void InvokeTrigger(int frame, double animationTimeCorrection);
		void AddTriggersInRange(List<Action> triggers, double minTime, double maxTime, bool inclusiveRange);

		void ResetCache();

		IKeyframeList ReadonlyKeys { get; }

		IKeyframeList Keys { get; }

		object UserData { get; set; }

		Type GetValueType();

		void BlendWith(IAnimator animator, float blendFactor);

		bool TryGetNextKeyFrame(int nextFrame, out int keyFrame);

		void Unbind();

		bool IsZombie { get; }

#if TANGERINE
		int Version { get; }
#endif // TANGERINE
	}

	public interface IKeyframeList : IList<IKeyframe>
	{
		IKeyframe CreateKeyframe();

		IKeyframe GetByFrame(int frame);

		void Add(int frame, object value, KeyFunction function = KeyFunction.Linear);
		void AddOrdered(int frame, object value, KeyFunction function = KeyFunction.Linear);
		void AddOrdered(IKeyframe keyframe);
#if TANGERINE
		int Version { get; }
#endif // TANGERINE
	}

	public class Animator<T> : IAnimator
	{
		public IAnimationHost Owner { get; set; }

		public IAnimable Animable
		{
			get {
				if (animable == null && !IsZombie) {
					Bind();
				}
				return animable;
			}
		}
		private IAnimable animable;
		private double minTime;
		private double maxTime;
		private KeyframeParams @params;
		private int keyIndex;
		protected T Value1, Value2, Value3, Value4;
		/// <summary>
		/// value stored since last CalcValue()
		/// </summary>
		public T CalculatedValue { get; protected set; }

		public bool IsTriggerable { get; set; }
		public bool Enabled { get; set; } = true;
		private delegate void SetterDelegate(T value);
		private delegate void IndexedSetterDelegate(int index, T value);
		private SetterDelegate setter;
		private bool isZombie;
		public bool IsZombie
		{
			get => isZombie;
			private set
			{
				isZombie = value;
#if TANGERINE
				version++;
#endif // TANGERINE
			}
		}

#if TANGERINE
		private int version;
		public int Version { get => version + ReadonlyKeys.Version; }
#endif // TANGERINE

		private string targetPropertyPath;
		[YuzuMember("TargetProperty")]
		public string TargetPropertyPath
		{
			get => targetPropertyPath;
			set {
				targetPropertyPath = value;
				TargetPropertyPathUID = TargetPropertyPathUIDGenerator.Generate(value);
			}
		}

		public int TargetPropertyPathUID { get; private set; }

		public Type GetValueType() { return typeof(T); }

		[YuzuMember]
		public TypedKeyframeList<T> ReadonlyKeys { get; private set; }

		[YuzuMember]
		public string AnimationId { get; set; }

		public object UserData { get; set; }

		public Animator()
		{
			ReadonlyKeys = new TypedKeyframeList<T>();
			ReadonlyKeys.AddRef();
		}

		public void Dispose()
		{
			ReadonlyKeys.Release();
		}

		public TypedKeyframeList<T> Keys
		{
			get
			{
				if (ReadonlyKeys.RefCount > 1) {
					ReadonlyKeys.Release();
					ReadonlyKeys = ReadonlyKeys.Clone();
					ReadonlyKeys.AddRef();
				}
				return ReadonlyKeys;
			}
		}

		IKeyframeList boxedKeys;
		IKeyframeList IAnimator.Keys
		{
			get
			{
				if (ReadonlyKeys.RefCount > 1) {
					boxedKeys = null;
				}
				if (boxedKeys == null) {
					boxedKeys = new BoxedKeyframeList<T>(Keys);
				}
				return boxedKeys;
			}
		}

		IKeyframeList IAnimator.ReadonlyKeys
		{
			get
			{
				if (boxedKeys == null) {
					boxedKeys = new BoxedKeyframeList<T>(ReadonlyKeys);
				}
				return boxedKeys;
			}
		}

		public IAnimator Clone()
		{
			var clone = (Animator<T>)MemberwiseClone();
			clone.setter = null;
#if TANGERINE
			clone.animable = null;
#endif // TANGERINE
			clone.IsZombie = false;
			clone.Owner = null;
			clone.boxedKeys = null;
			boxedKeys = null;
			ReadonlyKeys.AddRef();
			return clone;
		}

		public void Unbind()
		{
			IsZombie = false;
			setter = null;
#if TANGERINE
			animable = null;
#endif // TANGERINE
		}

		public int Duration => (ReadonlyKeys.Count == 0) ? 0 : ReadonlyKeys[ReadonlyKeys.Count - 1].Frame;

		protected virtual T InterpolateLinear(float t) => Value2;
		protected virtual T InterpolateSplined(float t) => InterpolateLinear(t);

		public virtual void BlendWith(IAnimator animator, float blendFactor)
		{
			if (blendFactor >= 0.5f) {
				CalculatedValue = ((Animator<T>)animator).CalculatedValue;
			}
		}

		public void Clear()
		{
			keyIndex = 0;
			Keys.Clear();
		}

		public void InvokeTrigger(int frame, double animationTimeCorrection)
		{
			if (!Enabled || IsZombie || !IsTriggerable) {
				return;
			}
			foreach (var key in ReadonlyKeys) {
				if (key.Frame == frame) {
					Owner?.OnTrigger(TargetPropertyPath, key.Value, animationTimeCorrection: animationTimeCorrection);
					break;
				}
			}
		}

		public void AddTriggersInRange(List<Action> triggers, double minTime, double maxTime, bool inclusiveRange)
		{
			if (!Enabled || IsZombie || !IsTriggerable) {
				return;
			}
			int minFrame = AnimationUtils.SecondsToFramesCeiling(minTime);
			int maxFrame = AnimationUtils.SecondsToFramesCeiling(maxTime) + (inclusiveRange ? 1 : 0);
			if (minFrame >= maxFrame) {
				return;
			}
			foreach (var key in ReadonlyKeys) {
				if (key.Frame >= maxFrame) {
					break;
				} else if (key.Frame >= minFrame) {
					var t = minTime - AnimationUtils.FramesToSeconds(key.Frame);
					triggers.Add(() => Owner?.OnTrigger(TargetPropertyPath, key.Value, animationTimeCorrection: t));
				}
			}
		}

		public void Apply()
		{
			if (Enabled && !IsZombie && ReadonlyKeys.Count > 0) {
				if (setter == null) {
					Bind();
					if (IsZombie) {
						return;
					}
				}
				setter(CalculatedValue);
			}
		}

		public void CalcAndApply(double time)
		{
			CalcValue(time);
			Apply();
		}

		private void Bind()
		{
			var (p, a, index) = AnimationUtils.GetPropertyByPath(Owner, TargetPropertyPath);
			var mi = p.Info?.GetSetMethod();
			IsZombie = a == null || mi == null || p.Info.PropertyType != typeof(T) || a is IList list && index >= list.Count;
			if (IsZombie) {
				return;
			}
#if TANGERINE
			animable = a;
#endif // TANGERINE
			IsTriggerable = p.Triggerable;
			if (index == -1) {
				setter = (SetterDelegate)Delegate.CreateDelegate(typeof(SetterDelegate), a, mi);
			} else {
				var indexedSetter = (IndexedSetterDelegate)Delegate.CreateDelegate(typeof(IndexedSetterDelegate), a, mi);
				setter = (v) => {
					indexedSetter(index, v);
				};
			}
		}

		public void ResetCache()
		{
			minTime = maxTime = 0;
		}

		public T CalcValue(double time)
		{
			if (time < minTime || time >= maxTime) {
				CacheInterpolationParameters(time);
			}
			if (@params.Function == KeyFunction.Steep) {
				CalculatedValue = Value2;
				return CalculatedValue;
			}
			var t = (float)((time - minTime) / (maxTime - minTime));
			if (@params.EasingFunction != EasingFunction.Linear) {
				t = Easing.Interpolate(t, @params.EasingFunction, @params.EasingType);
			}
			if (@params.Function == KeyFunction.Linear) {
				CalculatedValue = InterpolateLinear(t);
			} else {
				CalculatedValue = InterpolateSplined(t);
			}
			return CalculatedValue;
		}

		object IAnimator.CalcValue(double time) => CalcValue(time);

		public bool TryGetNextKeyFrame(int nextFrame, out int keyFrame)
		{
			foreach (var key in ReadonlyKeys) {
				if (key.Frame < nextFrame) {
					continue;
				}

				keyFrame = key.Frame;
				return true;
			}
			keyFrame = -1;
			return false;
		}

		private static KeyframeParams defaultKeyframeParams = new KeyframeParams {
			Function = KeyFunction.Steep,
			EasingFunction = EasingFunction.Linear
		};

		private void CacheInterpolationParameters(double time)
		{
			int count = ReadonlyKeys.Count;
			if (count == 0) {
				Value2 = default(T);
				minTime = -float.MaxValue;
				maxTime = float.MaxValue;
				@params = defaultKeyframeParams;
				return;
			}
			var i = keyIndex;
			if (i >= count) {
				i = count - 1;
			}
			int frame = AnimationUtils.SecondsToFrames(time);
			// find rightmost key on the left from the given frame
			while (i < count - 1 && frame > ReadonlyKeys[i].Frame) {
				i++;
			}
			while (i >= 0 && frame < ReadonlyKeys[i].Frame) {
				i--;
			}
			keyIndex = i;
			int minFrame, maxFrame;
			if (i < 0) {
				keyIndex = 0;
				maxFrame = ReadonlyKeys[0].Frame;
				minFrame = int.MinValue;
				Value2 = ReadonlyKeys[0].Value;
				@params = defaultKeyframeParams;
			} else if (i == count - 1) {
				minFrame = ReadonlyKeys[i].Frame;
				maxFrame = int.MaxValue;
				Value2 = ReadonlyKeys[i].Value;
				@params = defaultKeyframeParams;
			} else {
				var key1 = ReadonlyKeys[i];
				var key2 = ReadonlyKeys[i + 1];
				minFrame = key1.Frame;
				maxFrame = key2.Frame;
				Value2 = key1.Value;
				Value3 = key2.Value;
				@params = key1.Params;
				if (@params.Function == KeyFunction.Spline) {
					Value1 = ReadonlyKeys[i < 1 ? 0 : i - 1].Value;
					Value4 = ReadonlyKeys[i + 1 >= count - 1 ? count - 1 : i + 2].Value;
				} else if (@params.Function == KeyFunction.ClosedSpline) {
					Value1 = ReadonlyKeys[i < 1 ? count - 2 : i - 1].Value;
					Value4 = ReadonlyKeys[i + 1 >= count - 1 ? 1 : i + 2].Value;
				}
			}
			minTime = minFrame * AnimationUtils.SecondsPerFrame;
			maxTime = maxFrame * AnimationUtils.SecondsPerFrame;
		}

		internal static class TargetPropertyPathUIDGenerator
		{
			private static int counter = 1;
			private static ConcurrentDictionary<string, int> map = new ConcurrentDictionary<string, int>();

			public static int Generate(string targetPath)
			{
				return map.GetOrAdd(targetPath, _ => counter++);
			}
		}
	}

	public class Vector2Animator : Animator<Vector2>
	{
		protected override Vector2 InterpolateLinear(float t)
		{
			Vector2 r;
			r.X = Value2.X + (Value3.X - Value2.X) * t;
			r.Y = Value2.Y + (Value3.Y - Value2.Y) * t;
			return r;
		}

		protected override Vector2 InterpolateSplined(float t)
		{
			return new Vector2(
				Mathf.CatmullRomSpline(t, Value1.X, Value2.X, Value3.X, Value4.X),
				Mathf.CatmullRomSpline(t, Value1.Y, Value2.Y, Value3.Y, Value4.Y)
			);
		}

		public override void BlendWith(IAnimator animator, float blendFactor)
		{
			var otherValue = ((Vector2Animator)animator).CalculatedValue;
			CalculatedValue = Vector2.Lerp(blendFactor, CalculatedValue, otherValue);
		}
	}

	public class Vector3Animator : Animator<Vector3>
	{
		protected override Vector3 InterpolateLinear(float t)
		{
			return Vector3.Lerp(t, Value2, Value3);
		}

		protected override Vector3 InterpolateSplined(float t)
		{
			return Mathf.CatmullRomSpline(t, Value1, Value2, Value3, Value4);
		}

		public override void BlendWith(IAnimator animator, float blendFactor)
		{
			var otherValue = ((Vector3Animator)animator).CalculatedValue;
			CalculatedValue = Vector3.Lerp(blendFactor, CalculatedValue, otherValue);
		}
	}

	public class NumericAnimator : Animator<float>
	{
		protected override float InterpolateLinear(float t)
		{
			return t * (Value3 - Value2) + Value2;
		}

		protected override float InterpolateSplined(float t)
		{
			return Mathf.CatmullRomSpline(t, Value1, Value2, Value3, Value4);
		}

		public override void BlendWith(IAnimator animator, float blendFactor)
		{
			var otherValue = ((NumericAnimator)animator).CalculatedValue;
			CalculatedValue = Mathf.Lerp(blendFactor, CalculatedValue, otherValue);
		}
	}

	public class IntAnimator : Animator<int>
	{
		protected override int InterpolateLinear(float t)
		{
			return (t * (Value3 - Value2) + Value2).Round();
		}

		protected override int InterpolateSplined(float t)
		{
			return Mathf.CatmullRomSpline(t, Value1, Value2, Value3, Value4).Round();
		}

		public override void BlendWith(IAnimator animator, float blendFactor)
		{
			var otherValue = ((IntAnimator)animator).CalculatedValue;
			CalculatedValue = Mathf.Lerp(blendFactor, CalculatedValue, otherValue).Round();
		}
	}

	public class Color4Animator : Animator<Color4>
	{
		protected override Color4 InterpolateLinear(float t)
		{
			return Color4.Lerp(t, Value2, Value3);
		}

		public override void BlendWith(IAnimator animator, float blendFactor)
		{
			var otherValue = ((Color4Animator)animator).CalculatedValue;
			CalculatedValue = Color4.Lerp(blendFactor, CalculatedValue, otherValue);
		}
	}

	public class QuaternionAnimator : Animator<Quaternion>
	{
		protected override Quaternion InterpolateLinear(float t)
		{
			return Quaternion.Slerp(Value2, Value3, t);
		}

		public override void BlendWith(IAnimator animator, float blendFactor)
		{
			var otherValue = ((QuaternionAnimator)animator).CalculatedValue;
			CalculatedValue = Quaternion.Lerp(CalculatedValue, otherValue, blendFactor);
		}
	}

	public class Matrix44Animator : Animator<Matrix44>
	{
		protected override Matrix44 InterpolateLinear(float t)
		{
			return Matrix44.Lerp(Value2, Value3, t);
		}

		public override void BlendWith(IAnimator animator, float blendFactor)
		{
			var otherValue = ((Matrix44Animator)animator).CalculatedValue;
			CalculatedValue = Matrix44.Lerp(CalculatedValue, otherValue, blendFactor);
		}
	}

	public class ThicknessAnimator : Animator<Thickness>
	{
		protected override Thickness InterpolateLinear(float t)
		{
			Thickness r;
			r.Left = Value2.Left + (Value3.Left - Value2.Left) * t;
			r.Right = Value2.Right + (Value3.Right - Value2.Right) * t;
			r.Top = Value2.Top + (Value3.Top - Value2.Top) * t;
			r.Bottom = Value2.Bottom + (Value3.Bottom - Value2.Bottom) * t;
			return r;
		}

		protected override Thickness InterpolateSplined(float t)
		{
			return new Thickness(
				Mathf.CatmullRomSpline(t, Value1.Left, Value2.Left, Value3.Left, Value4.Left),
				Mathf.CatmullRomSpline(t, Value1.Right, Value2.Right, Value3.Right, Value4.Right),
				Mathf.CatmullRomSpline(t, Value1.Top, Value2.Top, Value3.Top, Value4.Top),
				Mathf.CatmullRomSpline(t, Value1.Bottom, Value2.Bottom, Value3.Bottom, Value4.Bottom)
			);
		}

		public override void BlendWith(IAnimator animator, float blendFactor)
		{
			var otherValue = ((ThicknessAnimator)animator).CalculatedValue;
			CalculatedValue = new Thickness(
				Mathf.Lerp(blendFactor, CalculatedValue.Left, otherValue.Left),
				Mathf.Lerp(blendFactor, CalculatedValue.Right, otherValue.Right),
				Mathf.Lerp(blendFactor, CalculatedValue.Top, otherValue.Top),
				Mathf.Lerp(blendFactor, CalculatedValue.Bottom, otherValue.Bottom)
			);
		}
	}

	public class NumericRangeAnimator : Animator<NumericRange>
	{
		protected override NumericRange InterpolateLinear(float t)
		{
			NumericRange r;
			r.Median = Value2.Median + (Value3.Median - Value2.Median) * t;
			r.Dispersion = Value2.Dispersion + (Value3.Dispersion - Value2.Dispersion) * t;
			return r;
		}

		protected override NumericRange InterpolateSplined(float t)
		{
			return new NumericRange(
				Mathf.CatmullRomSpline(t, Value1.Median, Value2.Median, Value3.Median, Value4.Median),
				Mathf.CatmullRomSpline(t, Value1.Dispersion, Value2.Dispersion, Value3.Dispersion, Value4.Dispersion)
			);
		}

		public override void BlendWith(IAnimator animator, float blendFactor)
		{
			var otherValue = ((NumericRangeAnimator)animator).CalculatedValue;
			CalculatedValue = new NumericRange(
				Mathf.Lerp(blendFactor, CalculatedValue.Median, otherValue.Median),
				Mathf.Lerp(blendFactor, CalculatedValue.Dispersion, otherValue.Dispersion)
			);
		}
	}
}
