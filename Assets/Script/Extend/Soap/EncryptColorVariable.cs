using CodeStage.AntiCheat.ObscuredTypes;
using CodeStage.AntiCheat.Storage;
using UnityEngine;

namespace Obvious.Soap
{
	/// <summary>
	/// Color 변수를 암호화하여 저장/로드하는 클래스
	/// </summary>
	[CreateAssetMenu(fileName = "scriptable_variable_encrypt_color.asset", menuName = "Soap/EncryptScriptableVariables/color")]
	public class EncryptColorVariable : ScriptableVariable<Color>
	{
		[SerializeField] private ObscuredFloat _obscuredR;
		[SerializeField] private ObscuredFloat _obscuredG;
		[SerializeField] private ObscuredFloat _obscuredB;
		[SerializeField] private ObscuredFloat _obscuredA;

		public override Color Value
		{
			get => new Color(_obscuredR, _obscuredG, _obscuredB, _obscuredA);
			set
			{
				if (!Mathf.Approximately(_obscuredR, value.r) ||
					!Mathf.Approximately(_obscuredG, value.g) ||
					!Mathf.Approximately(_obscuredB, value.b) ||
					!Mathf.Approximately(_obscuredA, value.a))
				{
					_obscuredR = value.r;
					_obscuredG = value.g;
					_obscuredB = value.b;
					_obscuredA = value.a;
					ValueChanged();
				}
			}
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			// ObscuredFloat 초기화
			if (_obscuredR.GetHashCode() == 0 && _obscuredG.GetHashCode() == 0 &&
				_obscuredB.GetHashCode() == 0 && _obscuredA.GetHashCode() == 0)
			{
				_obscuredR = DefaultValue.r;
				_obscuredG = DefaultValue.g;
				_obscuredB = DefaultValue.b;
				_obscuredA = DefaultValue.a;
			}
		}
		public override void Save()
		{
			// 객체 유효성 검사
			if (this == null) return;

			try
			{
				// AntiCheat Toolkit의 ObscuredPrefs 사용
				ObscuredPrefs.Set(Guid, Value);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
				Debug.Log($"암호화된 Color 저장 완료: {name} = {Value}");
#endif
			}
			catch (System.Exception e)
			{
				// 오류 발생 시 안전하게 처리
#if UNITY_EDITOR || DEVELOPMENT_BUILD
				Debug.LogError($"암호화된 Color 저장 실패: {e.Message}");
#endif
			}
		}

		public override void Load()
		{
			// 객체 유효성 검사
			if (this == null) return;

			try
			{
				// AntiCheat Toolkit의 ObscuredPrefs에서 로드
				Color loadedColor = ObscuredPrefs.Get(Guid, DefaultValue);
				_obscuredR = loadedColor.r;
				_obscuredG = loadedColor.g;
				_obscuredB = loadedColor.b;
				_obscuredA = loadedColor.a;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
				Debug.Log($"암호화된 Color 로드 완료: {name} = {Value}");
#endif
			}
			catch (System.Exception e)
			{
				// 오류 발생 시 기본값으로 복구
				_obscuredR = DefaultValue.r;
				_obscuredG = DefaultValue.g;
				_obscuredB = DefaultValue.b;
				_obscuredA = DefaultValue.a;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
				Debug.LogError($"암호화된 Color 로드 실패, 기본값으로 복구: {e.Message}");
#endif
			}
		}

		/// <summary>
		/// 아름다운 랜덤 색상 설정
		/// </summary>
		public void SetRandom()
		{
			if (this == null) return;
			Value = Random.ColorHSV(0f, 1f, 1f, 1f, 0.5f, 1f);
		}

		/// <summary>
		/// 색상의 밝기를 조절합니다
		/// </summary>
		public void AdjustBrightness(float factor)
		{
			if (this == null) return;
			Color color = Value;
			color.r = Mathf.Clamp01(color.r * factor);
			color.g = Mathf.Clamp01(color.g * factor);
			color.b = Mathf.Clamp01(color.b * factor);
			Value = color;
		}

		/// <summary>
		/// 투명도를 설정합니다
		/// </summary>
		public void SetAlpha(float alpha)
		{
			if (this == null) return;
			Color color = Value;
			color.a = Mathf.Clamp01(alpha);
			Value = color;
		}

		/// <summary>
		/// 색상을 반전시킵니다
		/// </summary>
		public void Invert()
		{
			if (this == null) return;
			Color color = Value;
			color.r = 1f - color.r;
			color.g = 1f - color.g;
			color.b = 1f - color.b;
			Value = color;
		}
	}
}
