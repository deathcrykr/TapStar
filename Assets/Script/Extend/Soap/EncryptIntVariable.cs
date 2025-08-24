using CodeStage.AntiCheat.ObscuredTypes;
using CodeStage.AntiCheat.Storage;
using UnityEngine;

namespace Obvious.Soap
{
	[CreateAssetMenu(fileName = "scriptable_variable_encrypt_int.asset", menuName = "Soap/EncryptScriptableVariables/int")]
	public class EncryptIntVariable : ScriptableVariable<int>
	{
		[SerializeField] private ObscuredInt _obscuredValue;

		public override int Value
		{
			get => _obscuredValue;
			set
			{
				if (_obscuredValue != value)
				{
					_obscuredValue = value;
					ValueChanged();
				}
			}
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			// ObscuredInt 초기화
			if (_obscuredValue.GetHashCode() == 0)
			{
				_obscuredValue = DefaultValue;
			}
		}

		public override void Load()
		{
			// 객체 유효성 검사
			if (this == null) return;

			try
			{
				// AntiCheat Toolkit의 ObscuredPrefs에서 로드
				_obscuredValue = ObscuredPrefs.Get(Guid, DefaultValue);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
				Debug.Log($"암호화된 Int 로드 완료: {name} = {_obscuredValue}");
#endif
			}
			catch (System.Exception e)
			{
				// 오류 발생 시 기본값으로 복구
				_obscuredValue = DefaultValue;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
				Debug.LogError($"암호화된 Int 로드 실패, 기본값으로 복구: {e.Message}");
#endif
			}
		}

		public override void Save()
		{
			// 객체 유효성 검사
			if (this == null) return;

			try
			{
				// AntiCheat Toolkit의 ObscuredPrefs 사용
				ObscuredPrefs.Set(Guid, (int)_obscuredValue);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
				Debug.Log($"암호화된 Int 저장 완료: {name} = {_obscuredValue}");
#endif
			}
			catch (System.Exception e)
			{
				// 오류 발생 시 안전하게 처리
#if UNITY_EDITOR || DEVELOPMENT_BUILD
				Debug.LogError($"암호화된 Int 저장 실패: {e.Message}");
#endif
			}
		}

		/// <summary>
		/// 값을 더합니다
		/// </summary>
		public void Add(int value)
		{
			// 객체 유효성 검사
			if (this == null) return;

			Value += value;
		}

		/// <summary>
		/// 값을 뺍니다
		/// </summary>
		public void Subtract(int value)
		{
			// 객체 유효성 검사
			if (this == null) return;

			Value -= value;
		}

		/// <summary>
		/// 값을 곱합니다
		/// </summary>
		public void Multiply(int value)
		{
			// 객체 유효성 검사
			if (this == null) return;

			Value *= value;
		}

		/// <summary>
		/// 값을 나눕니다 (0으로 나누기 방지)
		/// </summary>
		public void Divide(int value)
		{
			// 객체 유효성 검사
			if (this == null) return;

			if (value != 0)
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
		/// 절댓값을 설정합니다
		/// </summary>
		public void Abs()
		{
			// 객체 유효성 검사
			if (this == null) return;

			Value = System.Math.Abs(Value);
		}

		/// <summary>
		/// 값을 최소값과 최대값 사이로 제한합니다
		/// </summary>
		public void Clamp(int min, int max)
		{
			// 객체 유효성 검사
			if (this == null) return;

			Value = Mathf.Clamp(Value, min, max);
		}
	}
}
