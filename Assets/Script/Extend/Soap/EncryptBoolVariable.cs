using CodeStage.AntiCheat.ObscuredTypes;
using CodeStage.AntiCheat.Storage;
using UnityEngine;

namespace Obvious.Soap
{
	[CreateAssetMenu(fileName = "scriptable_variable_encrypt_bool.asset", menuName = "Soap/EncryptScriptableVariables/bool")]
	[System.Serializable]
	public class EncryptBoolVariable : ScriptableVariable<bool>
	{
		[SerializeField] private ObscuredBool _obscuredValue;

		public override bool Value
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
			// ObscuredBool 초기화
			if (_obscuredValue.GetHashCode() == 0)
			{
				_obscuredValue = DefaultValue;
			}
		}

		public override void Save()
		{
			// 객체 유효성 검사
			if (this == null) return;

			try
			{
				// AntiCheat Toolkit의 ObscuredPrefs 사용
				ObscuredPrefs.Set(Guid, (bool)_obscuredValue);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
				Debug.Log($"암호화된 Bool 저장 완료: {name} = {_obscuredValue}");
#endif
			}
			catch (System.Exception e)
			{
				// 오류 발생 시 안전하게 처리
#if UNITY_EDITOR || DEVELOPMENT_BUILD
				Debug.LogError($"암호화된 Bool 저장 실패: {e.Message}");
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
				_obscuredValue = ObscuredPrefs.Get(Guid, DefaultValue);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
				Debug.Log($"암호화된 Bool 로드 완료: {name} = {_obscuredValue}");
#endif
			}
			catch (System.Exception e)
			{
				// 오류 발생 시 기본값으로 복구
				_obscuredValue = DefaultValue;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
				Debug.LogError($"암호화된 Bool 로드 실패, 기본값으로 복구: {e.Message}");
#endif
			}
		}

		/// <summary>
		/// 값을 반전시킵니다 (true → false, false → true)
		/// </summary>
		public void Toggle()
		{
			// 객체 유효성 검사
			if (this == null) return;

			Value = !Value;
		}
	}
}
