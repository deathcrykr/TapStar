using CodeStage.AntiCheat.ObscuredTypes;
using CodeStage.AntiCheat.Storage;
using UnityEngine;

namespace Obvious.Soap
{
	/// <summary>
	/// Vector2 값을 암호화하여 저장/로드하는 클래스
	/// </summary>
	[CreateAssetMenu(fileName = "scriptable_variable_encrypt_vector2.asset", menuName = "Soap/EncryptScriptableVariables/vector2")]
	public class EncryptVector2Variable : ScriptableVariable<Vector2>
	{
		[SerializeField] private ObscuredVector2 _obscuredValue;

		public override Vector2 Value
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
			// ObscuredVector2 초기화
			if (_obscuredValue.GetHashCode() == 0)
			{
				_obscuredValue = DefaultValue;
			}
		}

		/// <summary>
		/// Vector2 값을 암호화하여 저장
		/// </summary>
		public override void Save()
		{
			// 객체 유효성 검사
			if (this == null) return;

			try
			{
				// AntiCheat Toolkit의 ObscuredPrefs 사용
				ObscuredPrefs.Set(Guid, (Vector2)_obscuredValue);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
				Debug.Log($"암호화된 Vector2 저장 완료: {name} = {_obscuredValue}");
#endif
			}
			catch (System.Exception e)
			{
				// 오류 발생 시 안전하게 처리
#if UNITY_EDITOR || DEVELOPMENT_BUILD
				Debug.LogError($"암호화된 Vector2 저장 실패: {e.Message}");
#endif
			}
		}

		/// <summary>
		/// 암호화된 Vector2 값을 불러옴
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
				Debug.Log($"암호화된 Vector2 로드 완료: {name} = {_obscuredValue}");
#endif
			}
			catch (System.Exception e)
			{
				// 오류 발생 시 기본값으로 복구
				_obscuredValue = DefaultValue;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
				Debug.LogError($"암호화된 Vector2 로드 실패, 기본값으로 복구: {e.Message}");
#endif
			}
		}

		/// <summary>
		/// Vector2에 다른 Vector2를 더함
		/// </summary>
		public void Add(Vector2 vector)
		{
			// 객체 유효성 검사
			if (this == null) return;

			Value += vector;
		}

		/// <summary>
		/// Vector2에서 다른 Vector2를 뺌
		/// </summary>
		public void Subtract(Vector2 vector)
		{
			// 객체 유효성 검사
			if (this == null) return;

			Value -= vector;
		}

		/// <summary>
		/// Vector2에 스칼라 값을 곱함
		/// </summary>
		public void Multiply(float scalar)
		{
			// 객체 유효성 검사
			if (this == null) return;

			Value *= scalar;
		}

		/// <summary>
		/// Vector2를 정규화함
		/// </summary>
		public void Normalize()
		{
			// 객체 유효성 검사
			if (this == null) return;

			Value = Value.normalized;
		}

		/// <summary>
		/// Vector2의 크기를 반환
		/// </summary>
		public float Magnitude()
		{
			return Value.magnitude;
		}
	}
}
