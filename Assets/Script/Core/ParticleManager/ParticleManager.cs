using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Coffee.UIExtensions;
using TapStar.Soap;
using UnityEngine;

namespace TapStar.Manager
{
	/// <summary>
	/// 파티클을 관리하는 매니저 클래스입니다.
	/// 파티클의 생성, 재생, 중지 및 메모리 관리를 담당합니다.
	/// </summary>
	public class ParticleManager : Singleton<ParticleManager>
	{
		[Header("파티클 기본 설정")]
		[Tooltip("파티클 데이터 ScriptableObject")]
		[SerializeField] private ParticleAssets m_ParticleData;

		/// <summary>
		/// 파티클 이름과 해당 파티클 리스트를 저장하는 딕셔너리입니다.
		/// </summary>
		private Dictionary<string, List<Particle>> m_ParticleDict = new();

		/// <summary>
		/// 생성된 파티클 플레이어들을 저장하는 리스트입니다.
		/// </summary>
		private List<TemporaryParticlePlayer> m_InstantiatedParticles = new();

		/// <summary>
		/// 마지막 클린업 시간을 추적합니다.
		/// </summary>
		private float m_LastCleanupTime = 0f;

		/// <summary>
		/// 클린업 간격 (초)
		/// </summary>
		private const float CLEANUP_INTERVAL = 5.0f;

		/// <summary>
		/// Update 메서드에서 주기적으로 클린업을 수행합니다.
		/// </summary>
		private void Update()
		{
			// 정기적인 클린업 수행 (5초마다)
			if (Time.time - m_LastCleanupTime > CLEANUP_INTERVAL)
			{
				CleanUP();
				m_LastCleanupTime = Time.time;
			}
		}

		/// <summary>
		/// OnDestroy에서 모든 리소스를 정리합니다.
		/// </summary>
		protected override void OnDestroy()
		{
			StopAllParticles();
			m_InstantiatedParticles.Clear();
			m_ParticleDict.Clear();
		}

		/// <summary>
		/// null인 파티클 플레이어와 이미 재생이 끝난 파티클 플레이어를 제거합니다.
		/// 메모리 누수 방지 및 성능 최적화를 위해 필요합니다.
		/// </summary>
		private void CleanUP()
		{
			// 컬렉션 수정 중에 오류 방지를 위한 임시 리스트
			int initialCount = m_InstantiatedParticles.Count;

			// 다음 조건의 파티클 제거:
			// 1. null인 파티클
			// 2. 재생이 끝났거나 중지된 파티클 (루프 파티클 제외)
			// 3. 파티클 시스템이 비활성화된 파티클
			// 4. 파티클 상태가 Stopped인 파티클
			m_InstantiatedParticles.RemoveAll(particle =>
				particle == null ||
				(particle != null && (
					!particle.gameObject.activeInHierarchy ||
					(particle.GetParticleState() == ParticleState.Stopped && !particle.gameObject.activeSelf) ||
					particle.GetParticleState() == ParticleState.Stopped ||
					!particle.IsPlaying
				))
			);

			// 디버그 정보 (개발 모드에서만 사용)
			if (initialCount != m_InstantiatedParticles.Count)
			{
#if UNITY_EDITOR
				Debug.Log($"[ParticleManager] 파티클 클린업: {initialCount - m_InstantiatedParticles.Count}개 제거됨. 남은 파티클: {m_InstantiatedParticles.Count}개");
#endif
			}
		}

		/// <summary>
		/// 파티클 에셋에서 파티클 딕셔너리를 초기화합니다.
		/// </summary>
		protected override void DataInit()
		{
			if (m_ParticleData == null)
			{
#if UNITY_EDITOR
				Debug.LogWarning("[ParticleManager] 파티클 데이터가 설정되지 않았습니다.");
#endif
				return;
			}

			// 딕셔너리 초기화
			m_ParticleDict.Clear();

			// 파티클 데이터를 딕셔너리에 추가
			foreach (Particle particle in m_ParticleData.Particles)
			{
				if (string.IsNullOrEmpty(particle.Name))
				{
#if UNITY_EDITOR
					Debug.LogWarning("[ParticleManager] 이름이 없는 파티클이 발견되었습니다.");
#endif
					continue;
				}

				if (particle.Prefab == null)
				{
#if UNITY_EDITOR
					Debug.LogWarning($"[ParticleManager] '{particle.Name}' 파티클의 프리팹이 없습니다.");
#endif
					continue;
				}

				if (!m_ParticleDict.ContainsKey(particle.Name))
				{
					m_ParticleDict[particle.Name] = new List<Particle>();
				}
				m_ParticleDict[particle.Name].Add(particle);
			}

#if UNITY_EDITOR
			Debug.Log($"[ParticleManager] 파티클 데이터 초기화 완료. 총 {m_ParticleDict.Count}개 파티클 타입 로드됨.");
#endif
		}

		/// <summary>
		/// 파티클을 생성하고 설정된 위치, 회전, 스케일로 반환합니다.
		/// </summary>
		private GameObject ParticleGetParticle(string particleName, Vector3? localScale,
			Vector3? localPosition, Quaternion? localRotation, Transform parentTransform = null)
		{
			return ParticleGetParticle(particleName, localScale, localPosition, localRotation, out _, parentTransform);
		}

		/// <summary>
		/// 파티클을 생성하고 UI 스케일 정보를 반환합니다.
		/// </summary>
		private GameObject ParticleGetParticle(string particleName, Vector3? localScale,
			Vector3? localPosition, Quaternion? localRotation, out int scaleUI, Transform parentTransform = null)
		{
			scaleUI = 20; // 기본 UI 스케일 값

			// 파티클 이름 체크
			if (string.IsNullOrEmpty(particleName))
			{
				Debug.LogError("[ParticleManager] 파티클 이름이 없습니다.");
				return null;
			}

			// 파티클 딕셔너리에서 파티클 검색
			if (!m_ParticleDict.ContainsKey(particleName))
			{
#if UNITY_EDITOR
				Debug.LogWarning($"[ParticleManager] '{particleName}' 파티클을 찾을 수 없습니다.");
#endif
				return null;
			}

			if (m_ParticleDict[particleName] == null || m_ParticleDict[particleName].Count == 0)
			{
#if UNITY_EDITOR
				Debug.LogWarning($"[ParticleManager] '{particleName}' 파티클 오브젝트가 비어있습니다.");
#endif
				return null;
			}

			// 랜덤으로 파티클 선택
			Particle particle = m_ParticleDict[particleName][UnityEngine.Random.Range(0, m_ParticleDict[particleName].Count)];
			if (particle.Prefab == null)
			{
#if UNITY_EDITOR
				Debug.LogWarning($"[ParticleManager] '{particleName}' 파티클 프리팹이 null입니다.");
#endif
				return null;
			}

			// UI 스케일 설정
			if (particle.ScaleUI != 0)
				scaleUI = particle.ScaleUI;

			// 위치 설정
			Vector3 position = localPosition ?? (particle.Position != Vector3.zero ? particle.Position : particle.Prefab.transform.localPosition);

			// 회전 설정
			Quaternion rotation = localRotation ?? (particle.Rotation != Quaternion.identity ? particle.Rotation : particle.Prefab.transform.localRotation);

			// 스케일 설정
			Vector3 scale = (localScale != null && localScale.Value != Vector3.zero) ? localScale.Value : (particle.Scale != Vector3.zero ? particle.Scale : particle.Prefab.transform.localScale);

			// 파티클 오브젝트 생성
			GameObject particleObject = Instantiate(particle.Prefab, parentTransform ?? transform);
			particleObject.name = "Particle_" + particleName;
			particleObject.transform.localScale = scale;
			particleObject.transform.localPosition = position;
			particleObject.transform.localRotation = rotation;

			return particleObject;
		}

		/// <summary>
		/// 월드 좌표계에서 파티클을 재생합니다.
		/// </summary>
		/// <param name="particleName">재생할 파티클 이름</param>
		/// <param name="position">위치</param>
		/// <param name="rotation">회전</param>
		/// <param name="isLoop">루프 여부</param>
		/// <param name="keyName">키 이름</param>
		/// <param name="layerName">레이어 이름</param>
		/// <returns>재생된 파티클 플레이어</returns>
		public TemporaryParticlePlayer PlayWorld(string particleName, Vector3 position, Quaternion? rotation = null, bool isLoop = false, string keyName = "", string layerName = "", int maxParticles = 0)
		{
			return PlayWorld(particleName, position, rotation, Vector3.zero, isLoop, keyName, layerName, maxParticles);
		}

		/// <summary>
		/// 월드 좌표계에서 파티클을 재생합니다.
		/// </summary>
		public TemporaryParticlePlayer PlayWorld(string particleName, Vector3? localPosition,
			Quaternion? localRotation, Vector3? localScale,
			bool isLoop = false, string keyName = "", string layerName = "", int maxParticles = 0)
		{
			GameObject particleObject = ParticleGetParticle(particleName, localScale, localPosition, localRotation);
			if (particleObject == null)
			{
				return null;
			}

			// TemporaryParticlePlayer 컴포넌트 추가
			TemporaryParticlePlayer particlePlayer = particleObject.AddComponent<TemporaryParticlePlayer>();

			// 레이어 설정
			if (!string.IsNullOrEmpty(layerName))
				particleObject.layer = LayerMask.NameToLayer(layerName);

			// 파티클 플레이어 설정 및 재생
			string effectiveName = !string.IsNullOrEmpty(keyName) ? keyName : particleName;
			particlePlayer.Play(particleObject, isLoop, effectiveName, maxParticles);

			// ParticleManager에게 중지 알림 구독
			particlePlayer.OnParticleStopped += OnParticleStopped;

			// 모든 파티클을 리스트에 추가 (루프 상관없이)
			m_InstantiatedParticles.Add(particlePlayer);

			return particlePlayer;
		}

		/// <summary>
		/// 특정 파티클 시스템을 시작합니다.
		/// </summary>
		/// <param name="name">파티클 이름</param>
		/// <param name="localPosition">위치 오프셋</param>
		/// <param name="localRotation">회전 오프셋</param>
		/// <param name="isLoop">루프 여부</param>
		/// <param name="parentTransform">부모 트랜스폼</param>
		/// <param name="keyName">키 이름</param>
		/// <param name="layerName">레이어 이름</param>
		/// <returns>생성된 파티클 플레이어</returns>
		public TemporaryParticlePlayer Play(string name, Vector3? localPosition, Quaternion? localRotation, bool isLoop = false, Transform parentTransform = null, string keyName = "", string layerName = "", int maxParticles = 0)
		{
			return Play(name, localPosition, localRotation, null, isLoop, parentTransform, keyName, layerName, maxParticles);
		}

		/// <summary>
		/// 특정 파티클 시스템을 시작합니다.
		/// </summary>
		public TemporaryParticlePlayer Play(string particleName, Vector3? localPosition,
			Quaternion? localRotation, Vector3? localScale,
			bool isLoop = false, Transform parentTransform = null,
			string keyName = "", string layerName = "", int maxParticles = 0)
		{
			GameObject particleObject = ParticleGetParticle(particleName, localScale, localPosition, localRotation, parentTransform);
			if (particleObject == null)
			{
				return null;
			}

			// TemporaryParticlePlayer 컴포넌트 추가
			TemporaryParticlePlayer particlePlayer = particleObject.AddComponent<TemporaryParticlePlayer>();

			// 레이어 설정
			if (!string.IsNullOrEmpty(layerName))
			{
				particleObject.layer = LayerMask.NameToLayer(layerName);
			}

			// 파티클 플레이어 설정 및 재생
			string effectiveName = !string.IsNullOrEmpty(keyName) ? keyName : particleName;
			particlePlayer.Play(particleObject, isLoop, effectiveName, maxParticles);

			// ParticleManager에게 중지 알림 구독
			particlePlayer.OnParticleStopped += OnParticleStopped;

			// 모든 파티클을 리스트에 추가 (루프 상관없이)
			m_InstantiatedParticles.Add(particlePlayer);

			return particlePlayer;
		}

		/// <summary>
		/// UI에서 파티클을 재생하고 지정된 타겟으로 모이도록 합니다.
		/// </summary>
		public TemporaryParticlePlayer PlayUIAttractor(string particleName, Vector3? localPosition,
			Quaternion? localRotate, GameObject UIObject, GameObject target,
			int? scale = null, float delay = 0.2f, float maxSpeed = 6, int maxParticles = 0)
		{
			int UIScale;
			GameObject particleObject = ParticleGetParticle(particleName, null, localPosition,
				localRotate, out UIScale, UIObject.transform);
			if (particleObject == null)
			{
				return null;
			}

			// 지정된 타겟으로 모이는 설정
			UIParticleAttractor uiAttractor = target.AddComponent<UIParticleAttractor>();
			uiAttractor.delay = delay;
			uiAttractor.maxSpeed = maxSpeed;
			uiAttractor.destinationRadius = 1;
			uiAttractor.AddParticleSystem(particleObject.GetComponentInChildren<ParticleSystem>());

			// UI 파티클 설정
			UIParticle uIParticle = particleObject.gameObject.AddComponent<UIParticle>();
			uIParticle.autoScalingMode = UIParticle.AutoScalingMode.Transform;
			uIParticle.scale = scale ?? UIScale;
			uIParticle.RefreshParticles();

			// TemporaryParticlePlayer 컴포넌트 추가
			TemporaryParticlePlayer particlePlayer = particleObject.AddComponent<TemporaryParticlePlayer>();
			particlePlayer.PlayUI(uIParticle, uiAttractor, false, maxParticles);

			// ParticleManager에게 중지 알림 구독
			particlePlayer.OnParticleStopped += OnParticleStopped;

			// 리스트에 추가
			m_InstantiatedParticles.Add(particlePlayer);

			return particlePlayer;
		}

		/// <summary>
		/// UI에서 파티클 효과를 재생합니다.
		/// </summary>
		public TemporaryParticlePlayer PlayUIEffect(string particleName, Vector3? localPosition, Quaternion? localRotate, GameObject UIObject, int? localScale = 1, bool isLoop = false, int maxParticles = 0)
		{
			int UIScale;
			GameObject particleObject = ParticleGetParticle(particleName, Vector3.one, localPosition,
				localRotate, out UIScale, UIObject.transform);
			if (particleObject == null)
			{
				return null;
			}

			// UI 파티클 설정
			UIParticle uIParticle = particleObject.gameObject.AddComponent<UIParticle>();
			uIParticle.autoScalingMode = UIParticle.AutoScalingMode.UIParticle;
			uIParticle.scale = localScale ?? UIScale;
			uIParticle.RefreshParticles();

			// TemporaryParticlePlayer 컴포넌트 추가
			TemporaryParticlePlayer particlePlayer = particleObject.AddComponent<TemporaryParticlePlayer>();
			particlePlayer.PlayUI(uIParticle, null, isLoop, maxParticles);

			// ParticleManager에게 중지 알림 구독
			particlePlayer.OnParticleStopped += OnParticleStopped;

			// 모든 파티클을 리스트에 추가 (루프 상관없이)
			m_InstantiatedParticles.Add(particlePlayer);

			return particlePlayer;
		}

		/// <summary>
		/// 에셋에서 특정 파티클 프리팹을 가져옵니다.
		/// </summary>
		public GameObject GetParticleInAsset(string name)
		{
			if (!m_ParticleDict.ContainsKey(name))
			{
#if UNITY_EDITOR
				Debug.LogWarning($"[ParticleManager] '{name}' 파티클을 찾을 수 없습니다.");
#endif
				return null;
			}

			Particle particle = m_ParticleDict[name][UnityEngine.Random.Range(0, m_ParticleDict[name].Count)];
			return particle.Prefab;
		}

		/// <summary>
		/// 특정 파티클 시스템을 중지합니다.
		/// </summary>
		/// <param name="name">파티클 이름</param>
		public void Stop(string name)
		{
			// 복사본 리스트 생성 (순회 중 리스트 수정 방지)
			var particlesToCheck = new List<TemporaryParticlePlayer>(m_InstantiatedParticles);

			foreach (var player in particlesToCheck)
			{
				if (player != null && player.ParticleName == name)
				{
					player.Stop();
				}
			}
		}

		/// <summary>
		/// 모든 파티클 시스템을 중지합니다.
		/// </summary>
		public void StopAllParticles()
		{
			// 복사본 리스트 생성 (순회 중 리스트 수정 방지)
			var particlesToStop = new List<TemporaryParticlePlayer>(m_InstantiatedParticles);

			foreach (var player in particlesToStop)
			{
				if (player != null)
				{
					player.Stop();
				}
			}

			// 리스트 초기화
			m_InstantiatedParticles.Clear();
		}

		/// <summary>
		/// 파티클이 중지되었을 때 호출되는 콜백 메서드입니다.
		/// </summary>
		/// <param name="particlePlayer">중지된 파티클 플레이어</param>
		private void OnParticleStopped(TemporaryParticlePlayer particlePlayer)
		{
			if (particlePlayer == null) return;

			// 리스트에서 파티클 플레이어 제거
			m_InstantiatedParticles.Remove(particlePlayer);
		}

		/// <summary>
		/// 파티클 시스템을 중지하고 필요한 처리를 합니다.
		/// </summary>
		/// <param name="obj">파티클 오브젝트</param>
		/// <param name="effect">파티클 모델</param>
		internal IEnumerator StopAnimation(GameObject obj, ParticleModel effect)
		{
			if (obj == null)
				yield break;

			ParticleSystem[] particleSystems = obj.GetComponentsInChildren<ParticleSystem>();
			float animationTime = 0f;
			foreach (var ps in particleSystems)
			{
				if (ps == null) continue;
				animationTime = Mathf.Max(animationTime, ps.main.duration + ps.main.startLifetime.constantMax);
				ps.Stop(); // 각 파티클 시스템 중지
			}

			yield return new WaitForSeconds(animationTime);

			if (effect.isCopy) // isCopy가 true인 경우에만 삭제
			{
				yield return new WaitWhile(() => particleSystems.Any(p => p != null && p.IsAlive(true))); // 파티클이 모두 소멸할 때까지 대기
				Destroy(obj);
			}
			else // isCopy가 false인 경우 원래 위치로 되돌리기
			{
				yield return new WaitWhile(() => particleSystems.Any(p => p != null && p.IsAlive(true))); // 파티클이 모두 소멸할 때까지 대기
			}

			// 애니메이션 종료 후 isPlaying을 false로 설정
			effect.isPlaying = false;
		}

		/// <summary>
		/// 현재 활성화된 파티클 개수를 반환합니다.
		/// </summary>
		public int GetActiveParticleCount()
		{
			CleanUP();
			return m_InstantiatedParticles.Count;
		}

		/// <summary>
		/// 특정 이름의 파티클이 현재 재생 중인지 확인합니다.
		/// 클린업을 수행하여 정확한 상태를 반환합니다.
		/// </summary>
		/// <param name="name">확인할 파티클 이름</param>
		/// <returns>재생 중이면 true, 아니면 false</returns>
		public bool IsPlaying(string name)
		{
			CleanUP(); // 상태 체크 전 클린업 수행
			return m_InstantiatedParticles.Any(p => p != null && p.ParticleName == name && p.IsPlaying);
		}

		/// <summary>
		/// 등록된 모든 파티클 목록을 반환합니다. (에디터에서 사용)
		/// </summary>
		public List<TemporaryParticlePlayer> GetAllParticles()
		{
			return new List<TemporaryParticlePlayer>(m_InstantiatedParticles);
		}

	}
}
