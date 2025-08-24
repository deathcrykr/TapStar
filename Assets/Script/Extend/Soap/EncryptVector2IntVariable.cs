using CodeStage.AntiCheat.ObscuredTypes;
using CodeStage.AntiCheat.Storage;
using UnityEngine;

namespace Obvious.Soap
{
	/// <summary>
	/// Vector2Int 값을 암호화하여 저장/로드하는 클래스
	/// </summary>
	[CreateAssetMenu(fileName = "scriptable_variable_encrypt_vector2Int.asset", menuName = "Soap/EncryptScriptableVariables/vector2Int")]
	public class EncryptVector2IntVariable : ScriptableVariable<Vector2Int>
	{
		[SerializeField] private ObscuredInt _obscuredX;
		[SerializeField] private ObscuredInt _obscuredY;

		public override Vector2Int Value
		{
			get => new Vector2Int(_obscuredX, _obscuredY);
			set
			{
				if (_obscuredX != value.x || _obscuredY != value.y)
				{
					_obscuredX = value.x;
					_obscuredY = value.y;
					ValueChanged();
				}
			}
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			// ObscuredInt 초기화
			if (_obscuredX.GetHashCode() == 0 && _obscuredY.GetHashCode() == 0)
			{
				_obscuredX = DefaultValue.x;
				_obscuredY = DefaultValue.y;
			}
		}
		/// <summary>
		/// Vector2Int 값을 암호화하여 저장
		/// </summary>
		public override void Save()
		{
			// 객체 유효성 검사
			if (this == null) return;

			try
			{
				// AntiCheat Toolkit의 ObscuredPrefs 사용
				ObscuredPrefs.Set(Guid, Value);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
				Debug.Log($"암호화된 Vector2Int 저장 완료: {name} = {Value}");
#endif
			}
			catch (System.Exception e)
			{
				// 오류 발생 시 안전하게 처리
#if UNITY_EDITOR || DEVELOPMENT_BUILD
				Debug.LogError($"암호화된 Vector2Int 저장 실패: {e.Message}");
#endif
			}
		}

		/// <summary>
		/// 암호화된 Vector2Int 값을 불러옴
		/// </summary>
		public override void Load()
		{
			// 객체 유효성 검사
			if (this == null) return;

			try
			{
				// AntiCheat Toolkit의 ObscuredPrefs에서 로드
				Vector2Int loadedVector = ObscuredPrefs.Get(Guid, DefaultValue);
				_obscuredX = loadedVector.x;
				_obscuredY = loadedVector.y;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
				Debug.Log($"암호화된 Vector2Int 로드 완료: {name} = {Value}");
#endif
			}
			catch (System.Exception e)
			{
				// 오류 발생 시 기본값으로 복구
				_obscuredX = DefaultValue.x;
				_obscuredY = DefaultValue.y;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
				Debug.LogError($"암호화된 Vector2Int 로드 실패, 기본값으로 복구: {e.Message}");
#endif
			}
		}

		/// <summary>
		/// Vector2Int에 다른 Vector2Int를 더함
		/// </summary>
		public void Add(Vector2Int vector)
		{
			// 객체 유효성 검사
			if (this == null) return;

			Value += vector;
		}

		/// <summary>
		/// Vector2Int에서 다른 Vector2Int를 뺌
		/// </summary>
		public void Subtract(Vector2Int vector)
		{
			// 객체 유효성 검사
			if (this == null) return;

			Value -= vector;
		}

		/// <summary>
		/// Vector2Int에 스칼라 값을 곱함
		/// </summary>
		public void Multiply(int scalar)
		{
			// 객체 유효성 검사
			if (this == null) return;

			Value *= scalar;
		}

		/// <summary>
		/// Vector2Int의 크기 제곱을 반환
		/// </summary>
		public int SqrMagnitude()
		{
			return Value.sqrMagnitude;
		}

		/// <summary>
		/// 절댓값을 설정합니다
		/// </summary>
		public void Abs()
		{
			// 객체 유효성 검사
			if (this == null) return;

			Value = new Vector2Int(Mathf.Abs(Value.x), Mathf.Abs(Value.y));
		}
	}
}
