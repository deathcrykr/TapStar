using CodeStage.AntiCheat.ObscuredTypes;
using CodeStage.AntiCheat.Storage;
using UnityEngine;

namespace Obvious.Soap
{
	/// <summary>
	/// 부동소수점 값을 암호화하여 저장/로드하는 클래스
	/// </summary>
	[CreateAssetMenu(fileName = "scriptable_variable_encrypt_float.asset", menuName = "Soap/EncryptScriptableVariables/float")]
	public class EncryptFloatVariable : ScriptableVariable<float>
	{
		[SerializeField] private ObscuredFloat _obscuredValue;

		public override float Value
		{
			get => _obscuredValue;
			set
			{
				if (!Mathf.Approximately(_obscuredValue, value))
				{
					_obscuredValue = value;
					ValueChanged();
				}
			}
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			// ObscuredFloat 초기화
			if (_obscuredValue.GetHashCode() == 0)
			{
				_obscuredValue = DefaultValue;
			}
		}

		/// <summary>
		/// 값을 암호화하여 저장
		/// </summary>
		public override void Save()
		{
			// 객체 유효성 검사
			if (this == null) return;

			try
			{
				// AntiCheat Toolkit의 ObscuredPrefs 사용
				ObscuredPrefs.Set(Guid, (float)_obscuredValue);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
				Debug.Log($"암호화된 Float 저장 완료: {name} = {_obscuredValue}");
#endif
			}
			catch (System.Exception e)
			{
				// 오류 발생 시 안전하게 처리
#if UNITY_EDITOR || DEVELOPMENT_BUILD
				Debug.LogError($"암호화된 Float 저장 실패: {e.Message}");
#endif
			}
		}

		/// <summary>
		/// 암호화된 값을 불러옴
		/// </summary>
		public override void Load()
		{
			// 객체 유효성 검사
			if (this == null) return;

			try
			{
				// AntiCheat Toolkit의 ObscuredPrefs에서 로드
				_obscuredValue = ObscuredPrefs.Get(Guid, DefaultValue);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
				Debug.Log($"암호화된 Float 로드 완료: {name} = {_obscuredValue}");
#endif
			}
			catch (System.Exception e)
			{
				// 오류 발생 시 기본값으로 복구
				_obscuredValue = DefaultValue;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
				Debug.LogError($"암호화된 Float 로드 실패, 기본값으로 복구: {e.Message}");
#endif
			}
		}

		/// <summary>
		/// 값을 증가시킴
		/// </summary>
		public void Add(float value)
		{
			// 객체 유효성 검사
			if (this == null) return;

			Value += value;
		}

		/// <summary>
		/// 값을 감소시킴
		/// </summary>
		public void Subtract(float value)
		{
			// 객체 유효성 검사
			if (this == null) return;

			Value -= value;
		}

		/// <summary>
		/// 값을 곱함
		/// </summary>
		public void Multiply(float value)
		{
			// 객체 유효성 검사
			if (this == null) return;

			Value *= value;
		}

		/// <summary>
		/// 값을 나눔 (0으로 나누기 방지)
		/// </summary>
		public void Divide(float value)
		{
			// 객체 유효성 검사
			if (this == null) return;

			if (!Mathf.Approximately(value, 0f))
			{
				Value /= value;
			}
			else
			{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
				Debug.LogWarning($"0으로 나누기 시도: {name}");
#endif
			}
		}

		/// <summary>
		/// 값을 최소값과 최대값 사이로 제한
		/// </summary>
		public void Clamp(float min, float max)
		{
			// 객체 유효성 검사
			if (this == null) return;

			Value = Mathf.Clamp(Value, min, max);
		}

		/// <summary>
		/// 값을 0과 1 사이로 제한
		/// </summary>
		public void Clamp01()
		{
			// 객체 유효성 검사
			if (this == null) return;

			Value = Mathf.Clamp01(Value);
		}
	}
}
