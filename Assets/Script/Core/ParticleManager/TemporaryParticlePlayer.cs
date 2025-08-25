/*
 * 파일명: TemporaryParticlePlayer.cs
 * 설명  : 파티클 및 UIParticle 재생, 부드러운 중지, 종료 후 오브젝트 제거 등 다양한 파티클 관련 기능을 제공하는 스크립트입니다.
 * 비고  : 기존 기능을 유지하면서 성능 개선 및 메모리 누수 문제를 해결하였습니다.
 */

using System;
using System.Collections;
using Coffee.UIExtensions;
using UnityEngine;

namespace TapStar.Manager
{
	/// <summary>
	/// 파티클 상태를 나타내는 열거형
	/// </summary>
	public enum ParticleState
	{
		/// <summary>대기 상태</summary>
		Ready,
		/// <summary>재생 중</summary>
		Playing,
		/// <summary>페이드 아웃 중</summary>
		FadingOut,
		/// <summary>정지됨</summary>
		Stopped,
		/// <summary>오류 상태</summary>
		Error
	}

	/// <summary>
	/// 파티클 시스템의 재생, 중지, 수명 관리를 담당하는 컴포넌트입니다.
	/// 일시적으로 사용되는 파티클 효과의 생명주기를 관리합니다.
	/// 성능 최적화를 위해 Update 메서드를 사용하지 않고 코루틴으로 구현했습니다.
	/// </summary>
	[RequireComponent(typeof(ParticleSystem))]
	public class TemporaryParticlePlayer : MonoBehaviour
	{
		#region 변수 및 프로퍼티

		[Header("파티클 정보")]
		[Tooltip("현재 파티클의 키 값 (식별자)")]
		private string m_ParticleName = "";

		/// <summary>
		/// 현재 파티클의 식별자 (키 값)
		/// </summary>
		public string ParticleName => m_ParticleName;

		[Tooltip("파티클 종료 시 호출되는 이벤트")]
		public event Action<TemporaryParticlePlayer> OnParticleStopped;

		[Header("내부 변수")]
		[Tooltip("자식 파티클 시스템 배열")]
		private ParticleSystem[] m_ParticleSystems;

		[Tooltip("UIParticle 컴포넌트 (UI 전용 파티클)")]
		private UIParticle m_uIParticle;

		[Tooltip("UIParticleAttractor 컴포넌트 (UI 파티클 애니메이션 관련)")]
		private UIParticleAttractor m_uiAttractor;

		[Tooltip("파티클 재생 여부 플래그")]
		private bool m_IsPlaying = false;

		/// <summary>
		/// 파티클이 현재 재생 중인지 여부를 반환합니다.
		/// </summary>
		public bool IsPlaying => m_IsPlaying;

		/// <summary>
		/// 총 재생 시간을 캐싱하여 성능을 향상시킵니다.
		/// </summary>
		private float m_CachedAnimationTime = 0f;

		/// <summary>
		/// 파티클 시스템의 총 재생 시간 (초)
		/// </summary>
		public float TotalDuration => m_CachedAnimationTime;

		/// <summary>
		/// 파티클 재생 시작 시간을 기록합니다.
		/// </summary>
		private float m_StartTime = 0f;

		/// <summary>
		/// 파티클이 재생된 후 경과한 시간 (초)
		/// </summary>
		public float ElapsedTime => Time.time - m_StartTime;

		/// <summary>
		/// 파티클 재생 진행률 (0.0 ~ 1.0)
		/// </summary>
		public float Progress => m_CachedAnimationTime > 0 ? Mathf.Clamp01(ElapsedTime / m_CachedAnimationTime) : 0f;

		/// <summary>
		/// 반복 재생 여부
		/// </summary>
		private bool m_IsLoop = false;

		/// <summary>
		/// 현재 파티클 상태
		/// </summary>
		private ParticleState m_State = ParticleState.Ready;

		/// <summary>
		/// 현재 파티클의 상태를 반환합니다.
		/// </summary>
		public ParticleState GetParticleState() => m_State;

		#endregion

		#region Unity 기본 메서드

		/// <summary>
		/// 컴포넌트가 초기화될 때 호출됩니다.
		/// </summary>
		private void Awake()
		{
			// 파티클 시스템 컴포넌트 캐싱
			InitializeParticleSystems();
		}

		/// <summary>
		/// 컴포넌트 활성화 시 호출됩니다.
		/// </summary>
		private void OnEnable()
		{
			// 상태 초기화
			m_State = ParticleState.Ready;
			m_IsPlaying = false;
		}

		// Update 메서드 제거 - 성능 최적화를 위해 코루틴으로 대체

		/// <summary>
		/// 게임 오브젝트가 파괴될 때 호출됩니다.
		/// 메모리 누수를 방지하기 위해 이벤트 구독을 해제합니다.
		/// </summary>
		private void OnDestroy()
		{
			// 재생 중일 경우 중지 이벤트 발생 (ParticleManager에게 알림)
			if (m_IsPlaying && m_State != ParticleState.Stopped)
			{
				TriggerStoppedEvent();
			}

			// 모든 코루틴 중지
			StopAllCoroutines();

			// 리소스 정리
			ReleaseResources();
		}

		#endregion

		#region 초기화 및 캐싱

		/// <summary>
		/// 파티클 시스템 컴포넌트를 찾아 캐싱합니다.
		/// </summary>
		private void InitializeParticleSystems()
		{
			if (m_ParticleSystems == null || m_ParticleSystems.Length == 0)
			{
				m_ParticleSystems = GetComponentsInChildren<ParticleSystem>(true);
			}
		}

		/// <summary>
		/// 파티클 시스템의 총 재생 시간을 계산하여 캐싱합니다.
		/// </summary>
		/// <returns>계산된 총 재생 시간 (초)</returns>
		private float CalculateTotalDuration()
		{
			float animationTime = 0f;

			if (m_ParticleSystems == null || m_ParticleSystems.Length == 0)
			{
				InitializeParticleSystems();
			}

			foreach (var ps in m_ParticleSystems)
			{
				if (ps == null) continue;
				var main = ps.main;
				float particleDuration = main.duration + main.startLifetime.constantMax;
				animationTime = Mathf.Max(animationTime, particleDuration);
			}

			return animationTime;
		}

		/// <summary>
		/// 리소스를 정리합니다. (메모리 누수 방지)
		/// </summary>
		private void ReleaseResources()
		{
			// 이벤트 구독 해제
			OnParticleStopped = null;

			// 배열 참조 해제
			m_ParticleSystems = null;

			// UI 컴포넌트 정리
			if (m_uiAttractor != null)
			{
				Destroy(m_uiAttractor);
				m_uiAttractor = null;
			}

			if (m_uIParticle != null)
			{
				Destroy(m_uIParticle);
				m_uIParticle = null;
			}
		}

		/// <summary>
		/// 중지 이벤트를 안전하게 발생시킵니다.
		/// </summary>
		private void TriggerStoppedEvent()
		{
			OnParticleStopped?.Invoke(this);
		}

		#endregion

		#region Public 메서드

		/// <summary>
		/// 모든 파티클 시스템의 수명 배수를 설정합니다.
		/// </summary>
		/// <param name="multiplier">수명 배수 값</param>
		public void setLifeTimeMultiplier(float multiplier)
		{
			if (m_ParticleSystems == null || m_ParticleSystems.Length == 0)
			{
				InitializeParticleSystems();
			}

			foreach (var ps in m_ParticleSystems)
			{
				if (ps == null) continue;

				var main = ps.main;
				main.startLifetimeMultiplier = multiplier;
			}

			// 수명이 변경되었으므로 총 재생 시간 재계산
			m_CachedAnimationTime = CalculateTotalDuration();
		}

		/// <summary>
		/// GameObject 내에 포함된 파티클 시스템들을 재생합니다.
		/// </summary>
		/// <param name="particles">파티클 GameObject</param>
		/// <param name="isLoop">반복 재생 여부</param>
		/// <param name="keyName">파티클 식별자 (키 값)</param>
		/// <param name="maxParticles">최대 파티클 수 (0이면 제한 없음)</param>
		public void Play(GameObject particles, bool isLoop, string keyName = "", int maxParticles = 0)
		{
			// 이미 재생 중인 경우 처리
			if (m_IsPlaying)
			{
				Stop(false);
			}

			// 파티클 시스템 초기화
			InitializeParticleSystems();

			// 상태 업데이트
			m_State = ParticleState.Playing;
			m_ParticleName = keyName;
			m_IsPlaying = true;
			m_IsLoop = isLoop;
			m_StartTime = Time.time;

			// 모든 파티클 시스템을 순회하며 설정
			foreach (var ps in m_ParticleSystems)
			{
				if (ps == null) continue;

				var main = ps.main;
				main.loop = isLoop;
				if (maxParticles > 0)
					main.maxParticles = maxParticles;

				// 이미 재생 중인 파티클은 재시작
				if (ps.isPlaying)
				{
					ps.Stop(true);
				}
				ps.Play();
			}

			// 총 재생 시간 계산 및 캐싱
			m_CachedAnimationTime = CalculateTotalDuration();

			// 루프가 아닐 경우, 애니메이션 종료 후 오브젝트 제거 코루틴 실행
			if (!isLoop)
			{
				StopAllCoroutines();
				StartCoroutine(COR_DestroyWhenFinish(m_CachedAnimationTime));
			}
		}

		/// <summary>
		/// UIParticle를 재생합니다. UI 전용 파티클을 활용하는 경우 사용합니다.
		/// </summary>
		/// <param name="uIParticle">UIParticle 컴포넌트</param>
		/// <param name="uiAttractor">UIParticleAttractor 컴포넌트 (선택 사항)</param>
		/// <param name="isLoop">반복 재생 여부</param>
		/// <param name="maxParticles">최대 파티클 수 (0이면 제한 없음)</param>
		public void PlayUI(UIParticle uIParticle, UIParticleAttractor uiAttractor = null, bool isLoop = false, int maxParticles = 0)
		{
			// 이미 재생 중인 경우 처리
			if (m_IsPlaying)
			{
				Stop(false);
			}

			// 상태 업데이트
			m_State = ParticleState.Playing;
			m_IsPlaying = true;
			m_IsLoop = isLoop;
			m_StartTime = Time.time;

			// UIParticle 컴포넌트 저장
			m_uIParticle = uIParticle;
			m_uiAttractor = uiAttractor;

			float animationTime = 0f;

			// UIParticle에 포함된 파티클 시스템을 순회하며 설정
			foreach (var ps in uIParticle.particles)
			{
				if (ps == null) continue;

				var main = ps.main;
				main.loop = isLoop;
				if (maxParticles > 0)
					main.maxParticles = maxParticles;

				float particleDuration = main.duration + main.startLifetime.constantMax;
				animationTime = Mathf.Max(animationTime, particleDuration);

				// 이미 재생 중인 파티클은 재시작
				if (ps.isPlaying)
				{
					ps.Stop(true);
				}
				ps.Play();
			}

			// 총 재생 시간 캐싱
			m_CachedAnimationTime = animationTime;

			// 루프가 아닐 경우, 일정 시간 후 파티클 제거 코루틴 실행
			if (!isLoop)
			{
				StopAllCoroutines();
				StartCoroutine(COR_DestroyWhenTime());
			}
		}

		/// <summary>
		/// 기존에 설정된 파티클 시스템들을 재생합니다.
		/// </summary>
		/// <param name="isLoop">반복 재생 여부</param>
		public void Play(bool isLoop)
		{
			if (m_ParticleSystems == null || m_ParticleSystems.Length == 0)
			{
				InitializeParticleSystems();

				if (m_ParticleSystems == null || m_ParticleSystems.Length == 0)
				{
					Debug.LogWarning("[TemporaryParticlePlayer] 재생할 파티클 시스템이 없습니다.");
					m_State = ParticleState.Error;
					return;
				}
			}

			// 상태 업데이트
			m_State = ParticleState.Playing;
			m_IsPlaying = true;
			m_IsLoop = isLoop;
			m_StartTime = Time.time;

			foreach (var ps in m_ParticleSystems)
			{
				if (ps == null || ps.isPlaying) continue;

				var main = ps.main;
				main.loop = isLoop;
				ps.Play();
			}

			// 총 재생 시간 계산 및 캐싱
			m_CachedAnimationTime = CalculateTotalDuration();

			// 루프가 아닐 경우, 애니메이션 종료 후 오브젝트 제거 코루틴 실행
			if (!isLoop)
			{
				StopAllCoroutines();
				StartCoroutine(COR_DestroyWhenFinish(m_CachedAnimationTime));
			}
		}

		/// <summary>
		/// 파티클 재생을 중지합니다.
		/// </summary>
		/// <param name="isDestroy">true이면 파티클 관련 오브젝트를 제거합니다.</param>
		public void Stop(bool isDestroy = true)
		{
			// 이미 중지된 상태면 무시
			if (m_State == ParticleState.Stopped)
				return;

			// 모든 코루틴 중지
			StopAllCoroutines();

			// 상태 업데이트
			m_State = ParticleState.Stopped;
			m_IsPlaying = false;

			// 파티클 시스템 중지
			if (m_ParticleSystems != null)
			{
				foreach (var ps in m_ParticleSystems)
				{
					if (ps != null && ps.isPlaying)
						ps.Stop(true); // 파티클 즉시 중지 및 제거
				}
			}

			// 중지 이벤트 발생
			TriggerStoppedEvent();

			if (isDestroy)
			{
				// UI 컴포넌트 제거
				if (m_uIParticle != null)
				{
					Destroy(m_uIParticle);
					m_uIParticle = null;
				}

				if (m_uiAttractor != null)
				{
					Destroy(m_uiAttractor);
					m_uiAttractor = null;
				}

				// 오브젝트 제거
				Destroy(gameObject);
			}
		}

		/// <summary>
		/// 지정한 시간 동안 파티클을 부드럽게 중지시키는 애니메이션을 진행한 후 중지합니다.
		/// </summary>
		/// <param name="duration">중지 애니메이션 지속 시간 (초)</param>
		public void SmoothStop(float duration = 2f)
		{
			if (!m_IsPlaying || m_State == ParticleState.Stopped || m_State == ParticleState.FadingOut)
				return;

			// 상태 업데이트
			m_State = ParticleState.FadingOut;

			StopAllCoroutines();
			StartCoroutine(COR_SmoothStop(duration));
		}

		/// <summary>
		/// 스케일을 점진적으로 줄이면서 파티클을 부드럽게 중지시키는 애니메이션을 진행한 후 중지합니다.
		/// </summary>
		/// <param name="duration">중지 애니메이션 지속 시간 (초)</param>
		public void SmoothStopWithScale(float duration = 2f)
		{
			if (!m_IsPlaying || m_State == ParticleState.Stopped || m_State == ParticleState.FadingOut)
				return;

			// 상태 업데이트
			m_State = ParticleState.FadingOut;

			StopAllCoroutines();
			StartCoroutine(COR_SmoothStopWithScale(duration));
		}

		#endregion

		#region 코루틴

		/// <summary>
		/// 파티클 애니메이션이 완료된 후 지정된 시간 후에 오브젝트를 제거하는 코루틴입니다.
		/// Update 메서드 대신 코루틴으로 파티클 상태를 관리합니다.
		/// </summary>
		/// <param name="animationTime">전체 파티클 애니메이션 지속 시간 (초)</param>
		private IEnumerator COR_DestroyWhenFinish(float animationTime)
		{
			// 안전성을 위해 최소 시간 설정
			animationTime = Mathf.Max(0.1f, animationTime);

			// 애니메이션 시간만큼 대기
			yield return new WaitForSeconds(animationTime);

			// 파티클 상태 체크 (Update 대체)
			bool shouldContinueChecking = true;
			float waitTime = 0f;
			float maxWaitTime = 2f; // 최대 추가 대기 시간

			// 모든 파티클이 완전히 사라질 때까지 주기적으로 체크
			while (shouldContinueChecking && waitTime < maxWaitTime)
			{
				bool allStopped = true;

				// 기본 파티클 시스템 체크
				if (m_ParticleSystems != null && m_ParticleSystems.Length > 0)
				{
					foreach (var ps in m_ParticleSystems)
					{
						if (ps != null && ps.IsAlive(true))
						{
							allStopped = false;
							break;
						}
					}
				}

				// UI 파티클 시스템 체크
				if (allStopped && m_uIParticle != null && m_uIParticle.particles != null && m_uIParticle.particles.Count > 0)
				{
					foreach (var ps in m_uIParticle.particles)
					{
						if (ps != null && ps.IsAlive(true))
						{
							allStopped = false;
							break;
						}
					}
				}

				// 모든 파티클이 중지되었으면 루프 종료
				if (allStopped)
				{
					shouldContinueChecking = false;
				}
				else
				{
					// 잠시 대기 후 다시 체크
					yield return new WaitForSeconds(0.1f);
					waitTime += 0.1f;
				}
			}

			// 파티클 중지 및 삭제
			Stop();
		}

		/// <summary>
		/// UIParticle가 재생 중일 때, 일정 시간 후 파티클 관련 오브젝트를 제거하는 코루틴입니다.
		/// Update 메서드 대신 코루틴으로 파티클 상태를 관리합니다.
		/// </summary>
		private IEnumerator COR_DestroyWhenTime()
		{
			// UI 파티클 시스템의, 재생 시간을 기반으로 대기 시간 설정
			float waitTime = m_CachedAnimationTime;

			// 안전을 위한 최소 대기 시간
			waitTime = Mathf.Max(1f, waitTime);

			// 지정된 시간 대기
			yield return new WaitForSeconds(waitTime);

			// 추가 대기 시간 설정 (모든 파티클이 화면에서 사라질 때까지)
			if (m_uIParticle != null && m_uIParticle.particles != null && m_uIParticle.particles.Count > 0)
			{
				bool allStopped = false;
				float extraWaitTime = 0f;
				float maxExtraWaitTime = 2f; // 최대 추가 대기 시간

				while (!allStopped && extraWaitTime < maxExtraWaitTime)
				{
					allStopped = true;
					foreach (var ps in m_uIParticle.particles)
					{
						if (ps != null && ps.IsAlive(true))
						{
							allStopped = false;
							break;
						}
					}

					if (!allStopped)
					{
						yield return new WaitForSeconds(0.1f);
						extraWaitTime += 0.1f;
					}
				}
			}

			// 파티클 중지 및 삭제
			Stop();
		}

		/// <summary>
		/// 파티클의 에미션 속도를 점진적으로 감소시켜 부드럽게 중지시키는 코루틴입니다.
		/// </summary>
		/// <param name="duration">애니메이션 지속 시간 (초)</param>
		private IEnumerator COR_SmoothStop(float duration)
		{
			if (m_ParticleSystems == null || m_ParticleSystems.Length == 0)
			{
				m_State = ParticleState.Stopped;
				Stop();
				yield break;
			}

			// 각 파티클 시스템의 초기 에미션 속도를 캐싱합니다.
			float[] initialRateOverTime = new float[m_ParticleSystems.Length];
			float[] initialRateOverDistance = new float[m_ParticleSystems.Length];

			for (int i = 0; i < m_ParticleSystems.Length; i++)
			{
				if (m_ParticleSystems[i] == null) continue;

				var emission = m_ParticleSystems[i].emission;
				initialRateOverTime[i] = emission.rateOverTimeMultiplier;
				initialRateOverDistance[i] = emission.rateOverDistanceMultiplier;
			}

			float elapsed = 0f;
			while (elapsed < duration)
			{
				// SmoothStep 함수를 사용하여 감속 효과를 부드럽게 적용
				float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

				for (int i = 0; i < m_ParticleSystems.Length; i++)
				{
					if (m_ParticleSystems[i] == null) continue;

					var emission = m_ParticleSystems[i].emission;
					emission.rateOverTimeMultiplier = Mathf.Lerp(initialRateOverTime[i], 0f, t);
					emission.rateOverDistanceMultiplier = Mathf.Lerp(initialRateOverDistance[i], 0f, t);
				}
				elapsed += Time.deltaTime;
				yield return null;
			}

			// 추가 대기 시간 설정 (기존 파티클이 모두 사라질 때까지)
			float additionalWaitTime = 0f;
			foreach (var ps in m_ParticleSystems)
			{
				if (ps == null) continue;

				var main = ps.main;
				additionalWaitTime = Mathf.Max(additionalWaitTime, main.startLifetime.constantMax);
			}

			// 남은 파티클이 사라질 때까지 대기
			if (additionalWaitTime > 0)
			{
				yield return new WaitForSeconds(additionalWaitTime);
			}

			Stop();
		}

		/// <summary>
		/// 스케일과 파티클 에미션 속도를 점진적으로 감소시켜 부드럽게 중지시키는 코루틴입니다.
		/// </summary>
		/// <param name="duration">애니메이션 지속 시간 (초)</param>
		private IEnumerator COR_SmoothStopWithScale(float duration)
		{
			if (m_ParticleSystems == null || m_ParticleSystems.Length == 0)
			{
				m_State = ParticleState.Stopped;
				Stop();
				yield break;
			}

			// 각 파티클 시스템의 초기 에미션 속도를 캐싱합니다.
			float[] initialRateOverTime = new float[m_ParticleSystems.Length];
			float[] initialRateOverDistance = new float[m_ParticleSystems.Length];

			for (int i = 0; i < m_ParticleSystems.Length; i++)
			{
				if (m_ParticleSystems[i] == null) continue;

				var emission = m_ParticleSystems[i].emission;
				initialRateOverTime[i] = emission.rateOverTimeMultiplier;
				initialRateOverDistance[i] = emission.rateOverDistanceMultiplier;
			}

			float elapsed = 0f;
			Vector3 initialScale = transform.localScale;

			while (elapsed < duration)
			{
				// SmoothStep 함수를 사용하여 느리게 시작해 빨라졌다가 다시 느리게 종료하는 이징 효과 적용
				float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

				// 오브젝트의 스케일을 점진적으로 줄임
				transform.localScale = Vector3.Lerp(initialScale, Vector3.zero, t);

				for (int i = 0; i < m_ParticleSystems.Length; i++)
				{
					if (m_ParticleSystems[i] == null) continue;

					var emission = m_ParticleSystems[i].emission;
					emission.rateOverTimeMultiplier = Mathf.Lerp(initialRateOverTime[i], 0f, t);
					emission.rateOverDistanceMultiplier = Mathf.Lerp(initialRateOverDistance[i], 0f, t);
				}

				elapsed += Time.deltaTime;
				yield return null;
			}

			// 최종적으로 스케일을 0으로 설정한 후 파티클을 중지합니다.
			transform.localScale = Vector3.zero;
			Stop();
		}

		#endregion
	}
}
