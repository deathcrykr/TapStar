using CodeStage.AntiCheat.ObscuredTypes;
using CodeStage.AntiCheat.Storage;
using UnityEngine;

namespace Obvious.Soap
{
	/// <summary>
	/// 문자열 값을 암호화하여 저장/로드하는 클래스
	/// </summary>
	[CreateAssetMenu(fileName = "scriptable_variable_encrypt_string.asset", menuName = "Soap/EncryptScriptableVariables/string")]
	public class EncryptStringVariable : ScriptableVariable<string>
	{
		[SerializeField] private ObscuredString _obscuredValue;

		public override string Value
		{
			get => _obscuredValue ?? "";
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
			// ObscuredString 초기화
			if (string.IsNullOrEmpty(_obscuredValue))
			{
				_obscuredValue = DefaultValue ?? "";
			}
		}

		/// <summary>
		/// 문자열 값을 암호화하여 저장
		/// </summary>
		public override void Save()
		{
			// 객체 유효성 검사
			if (this == null) return;

			try
			{
				// AntiCheat Toolkit의 ObscuredPrefs 사용
				ObscuredPrefs.Set(Guid, (string)_obscuredValue);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
				Debug.Log($"암호화된 String 저장 완료: {name} = {_obscuredValue}");
#endif
			}
			catch (System.Exception e)
			{
				// 오류 발생 시 안전하게 처리
#if UNITY_EDITOR || DEVELOPMENT_BUILD
				Debug.LogError($"암호화된 String 저장 실패: {e.Message}");
#endif
			}
		}

		/// <summary>
		/// 암호화된 문자열 값을 불러옴
		/// </summary>
		public override void Load()
		{
			// 객체 유효성 검사
			if (this == null) return;

			try
			{
				// AntiCheat Toolkit의 ObscuredPrefs에서 로드
				_obscuredValue = ObscuredPrefs.Get(Guid, DefaultValue ?? "");

#if UNITY_EDITOR || DEVELOPMENT_BUILD
				Debug.Log($"암호화된 String 로드 완료: {name} = {_obscuredValue}");
#endif
			}
			catch (System.Exception e)
			{
				// 오류 발생 시 기본값으로 복구
				_obscuredValue = DefaultValue ?? "";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
				Debug.LogError($"암호화된 String 로드 실패, 기본값으로 복구: {e.Message}");
#endif
			}
		}

		/// <summary>
		/// 문자열에 다른 문자열을 추가
		/// </summary>
		public void Append(string text)
		{
			// 객체 유효성 검사
			if (this == null) return;

			Value += text;
		}

		/// <summary>
		/// 문자열을 비움
		/// </summary>
		public void Clear()
		{
			// 객체 유효성 검사
			if (this == null) return;

			Value = "";
		}

		/// <summary>
		/// 문자열이 비어있는지 확인
		/// </summary>
		public bool IsEmpty()
		{
			return string.IsNullOrEmpty(Value);
		}

		/// <summary>
		/// 문자열의 길이를 반환
		/// </summary>
		public int Length()
		{
			return Value?.Length ?? 0;
		}
	}
}
