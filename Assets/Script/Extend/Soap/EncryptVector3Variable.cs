using CodeStage.AntiCheat.ObscuredTypes;
using CodeStage.AntiCheat.Storage;
using UnityEngine;

namespace Obvious.Soap
{
	[CreateAssetMenu(fileName = "scriptable_variable_encrypt_vector3.asset", menuName = "Soap/EncryptScriptableVariables/vector3")]
	public class EncryptVector3Variable : ScriptableVariable<Vector3>
	{
		[SerializeField] private ObscuredVector3 _obscuredValue;

		public override Vector3 Value
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
			// ObscuredVector3 초기화
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
				ObscuredPrefs.Set(Guid, (Vector3)_obscuredValue);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
				Debug.Log($"암호화된 Vector3 저장 완료: {name} = {_obscuredValue}");
#endif
			}
			catch (System.Exception e)
			{
				// 오류 발생 시 안전하게 처리
#if UNITY_EDITOR || DEVELOPMENT_BUILD
				Debug.LogError($"암호화된 Vector3 저장 실패: {e.Message}");
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
				Debug.Log($"암호화된 Vector3 로드 완료: {name} = {_obscuredValue}");
#endif
			}
			catch (System.Exception e)
			{
				// 오류 발생 시 기본값으로 복구
				_obscuredValue = DefaultValue;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
				Debug.LogError($"암호화된 Vector3 로드 실패, 기본값으로 복구: {e.Message}");
#endif
			}
		}

		/// <summary>
		/// Vector3에 다른 Vector3를 더함
		/// </summary>
		public void Add(Vector3 vector)
		{
			// 객체 유효성 검사
			if (this == null) return;

			Value += vector;
		}

		/// <summary>
		/// Vector3에서 다른 Vector3를 뺌
		/// </summary>
		public void Subtract(Vector3 vector)
		{
			// 객체 유효성 검사
			if (this == null) return;

			Value -= vector;
		}

		/// <summary>
		/// Vector3에 스칼라 값을 곱함
		/// </summary>
		public void Multiply(float scalar)
		{
			// 객체 유효성 검사
			if (this == null) return;

			Value *= scalar;
		}

		/// <summary>
		/// Vector3를 정규화함
		/// </summary>
		public void Normalize()
		{
			// 객체 유효성 검사
			if (this == null) return;

			Value = Value.normalized;
		}

		/// <summary>
		/// Vector3의 크기를 반환
		/// </summary>
		public float Magnitude()
		{
			return Value.magnitude;
		}

		/// <summary>
		/// 두 점 사이의 거리를 반환
		/// </summary>
		public float DistanceTo(Vector3 other)
		{
			return Vector3.Distance(Value, other);
		}

		/// <summary>
		/// 벡터를 회전시킴
		/// </summary>
		public void RotateAround(Vector3 axis, float angle)
		{
			// 객체 유효성 검사
			if (this == null) return;

			Value = Quaternion.AngleAxis(angle, axis) * Value;
		}
	}
}
