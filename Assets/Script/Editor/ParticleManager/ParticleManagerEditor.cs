using System.Collections.Generic;
using System.Linq;
using TapStar.Manager;
using TapStar.Soap;
using UnityEditor;
using UnityEngine;

namespace TapStar.Editors
{
	/// <summary>
	/// ParticleManager 컴포넌트의 에디터 커스터마이징을 위한 클래스
	/// </summary>
	[CustomEditor(typeof(ParticleManager))]
	public class ParticleManagerEditor : UnityEditor.Editor
	{
		#region 변수

		// 직렬화된 속성 참조
		private SerializedProperty m_ParticleDataProp;
		private SerializedProperty m_CleanupIntervalProp;

		// 아이콘
		private GUIContent m_ParticleIcon;
		private GUIContent m_InfoIcon;
		private GUIContent m_WarningIcon;

		// 상태 아이콘
		private Dictionary<ParticleState, GUIContent> m_StateIcons = new Dictionary<ParticleState, GUIContent>();

		// 활성화된 파티클 표시를 위한 스타일
		private GUIStyle m_ParticleCountStyle;
		private GUIStyle m_HeaderStyle;

		// 파티클 목록 스크롤 위치
		private Vector2 m_ScrollPosition;

		// 파티클 이름 필터
		private string m_ParticleNameFilter = "";

		// 필터 적용 여부
		private bool m_IsFilterActive = false;

		// 활성 파티클 목록 섹션 표시 여부
		private bool m_ShowActiveParticles = true;

		// 통계 정보 섹션 표시 여부
		private bool m_ShowStatistics = true;

		// 설정 섹션 표시 여부
		private bool m_ShowSettings = true;

		// 마지막 업데이트 시간
		private float m_LastUpdateTime = 0f;

		// 업데이트 간격 (초)
		private const float UPDATE_INTERVAL = 0.05f;

		// 캐시된 활성 파티클 목록
		private List<TemporaryParticlePlayer> m_CachedParticles;

		#endregion

		#region 초기화 메서드

		private void OnEnable()
		{
			// 직렬화된 속성 가져오기
			m_ParticleDataProp = serializedObject.FindProperty("m_ParticleData");

			// 아이콘 로드
			m_ParticleIcon = EditorGUIUtility.IconContent("ParticleSystem Icon");
			m_InfoIcon = EditorGUIUtility.IconContent("console.infoicon.sml");
			m_WarningIcon = EditorGUIUtility.IconContent("console.warnicon.sml");

			// 상태 아이콘 초기화
			InitializeStateIcons();

			// 스타일 초기화
			m_HeaderStyle = new GUIStyle(EditorStyles.boldLabel)
			{
				fontSize = 13,
				alignment = TextAnchor.MiddleLeft,
				margin = new RectOffset(5, 5, 5, 5)
			};

			// 파티클 수 표시를 위한 스타일
			m_ParticleCountStyle = new GUIStyle(EditorStyles.boldLabel)
			{
				fontSize = 12,
				alignment = TextAnchor.MiddleRight
			};

			// 에디터 업데이트 구독
			EditorApplication.update += OnEditorUpdate;
		}

		/// <summary>
		/// 상태 아이콘을 초기화합니다.
		/// </summary>
		private void InitializeStateIcons()
		{
			// Ready 상태 아이콘
			var readyIcon = new GUIContent(EditorGUIUtility.IconContent("d_WaitSpin00"));
			readyIcon.tooltip = "준비됨: 파티클이 재생 준비 상태입니다.";
			m_StateIcons[ParticleState.Ready] = readyIcon;

			// Playing 상태 아이콘
			var playingIcon = new GUIContent(EditorGUIUtility.IconContent("d_PlayButton"));
			playingIcon.tooltip = "재생 중: 파티클이 현재 재생 중입니다.";
			m_StateIcons[ParticleState.Playing] = playingIcon;

			// FadingOut 상태 아이콘
			var fadingIcon = new GUIContent(EditorGUIUtility.IconContent("d_RotateTool"));
			fadingIcon.tooltip = "페이드 아웃: 파티클이 서서히 사라지는 중입니다.";
			m_StateIcons[ParticleState.FadingOut] = fadingIcon;

			// Stopped 상태 아이콘
			var stoppedIcon = new GUIContent(EditorGUIUtility.IconContent("d_PauseButton"));
			stoppedIcon.tooltip = "중지됨: 파티클이 중지되었습니다.";
			m_StateIcons[ParticleState.Stopped] = stoppedIcon;

			// Error 상태 아이콘
			var errorIcon = new GUIContent(EditorGUIUtility.IconContent("d_console.erroricon.sml"));
			errorIcon.tooltip = "오류: 파티클 시스템에 오류가 발생했습니다.";
			m_StateIcons[ParticleState.Error] = errorIcon;
		}

		private void OnDisable()
		{
			// 에디터 업데이트 구독 해제
			EditorApplication.update -= OnEditorUpdate;
		}

		/// <summary>
		/// 에디터 업데이트 이벤트 핸들러
		/// </summary>
		private void OnEditorUpdate()
		{
			// 일정 간격으로 업데이트
			if (Time.realtimeSinceStartup - m_LastUpdateTime > UPDATE_INTERVAL)
			{
				if (Application.isPlaying && target != null)
				{
					// 파티클 목록 캐시 업데이트
					m_CachedParticles = GetActiveParticles((ParticleManager)target);

					// 에디터 갱신
					Repaint();
				}

				m_LastUpdateTime = Time.realtimeSinceStartup;
			}
		}

		#endregion

		#region GUI 렌더링

		public override void OnInspectorGUI()
		{
			// 대상 컴포넌트 가져오기
			ParticleManager particleManager = (ParticleManager)target;

			// 직렬화 객체 업데이트
			serializedObject.Update();

			// 타이틀과 설명
			EditorGUILayout.BeginVertical(GUI.skin.box);
			GUILayout.Label("Particle Manager", EditorStyles.boldLabel);
			EditorGUILayout.HelpBox("파티클 시스템을 중앙에서 관리합니다.\n파티클 에셋을 설정하고 활성화된 파티클을 확인할 수 있습니다.", MessageType.Info);
			EditorGUILayout.EndVertical();

			EditorGUILayout.Space(10);

			// 설정 섹션
			m_ShowSettings = EditorGUILayout.Foldout(m_ShowSettings, "설정", true, EditorStyles.foldoutHeader);
			if (m_ShowSettings)
			{
				EditorGUILayout.BeginVertical(EditorStyles.helpBox);

				// 기본 프로퍼티 표시
				EditorGUILayout.PropertyField(m_ParticleDataProp, new GUIContent("파티클 데이터", "파티클 에셋 목록을 포함하는 ScriptableObject입니다."));

				// CLEANUP_INTERVAL은 const 필드이므로 읽기 전용으로 표시
				float cleanupInterval = 5f; // 기본값

				// Reflection을 통해 const 값 가져오기 시도
				try
				{
					var field = typeof(ParticleManager).GetField("CLEANUP_INTERVAL",
						System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
					if (field != null)
					{
						cleanupInterval = (float)field.GetValue(null);
					}
				}
				catch (System.Exception)
				{
					// 실패 시 기본값 유지
				}

				EditorGUI.BeginDisabledGroup(true); // 읽기 전용으로 설정
				EditorGUILayout.FloatField(new GUIContent("정리 간격 (초)", "파티클 자동 정리 간격입니다. 이 값은 상수로 정의되어 있습니다."), cleanupInterval);
				EditorGUI.EndDisabledGroup();

				EditorGUILayout.EndVertical();
			}

			EditorGUILayout.Space(5);

			// 통계 정보 섹션
			m_ShowStatistics = EditorGUILayout.Foldout(m_ShowStatistics, "통계 정보", true, EditorStyles.foldoutHeader);
			if (m_ShowStatistics)
			{
				EditorGUILayout.BeginVertical(EditorStyles.helpBox);

				int totalParticles = particleManager.GetActiveParticleCount();

				EditorGUILayout.LabelField($"활성화된 파티클: {totalParticles}개");

				// 파티클 타입별 개수 (리플렉션으로 m_ParticleDict 접근)
				try
				{
					var dictField = typeof(ParticleManager).GetField("m_ParticleDict",
						System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

					if (dictField != null)
					{
						var dict = dictField.GetValue(particleManager) as System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<Particle>>;
						if (dict != null)
						{
							EditorGUILayout.LabelField($"등록된 파티클 유형: {dict.Count}개");
						}
					}
				}
				catch (System.Exception)
				{
					// 실패 시 무시
				}

				EditorGUILayout.EndVertical();
			}

			EditorGUILayout.Space(5);

			// 활성 파티클 섹션
			var activeParticlesContent = new GUIContent("활성 파티클 목록", "현재 활성화된 모든 파티클을 표시합니다.");
			m_ShowActiveParticles = EditorGUILayout.Foldout(m_ShowActiveParticles, activeParticlesContent, true, EditorStyles.foldoutHeader);
			if (m_ShowActiveParticles)
			{
				EditorGUILayout.BeginVertical(EditorStyles.helpBox);

				// 파티클 이름 필터
				EditorGUILayout.BeginHorizontal();
				GUILayout.Label(new GUIContent("필터:", "파티클 이름으로 목록을 필터링합니다."), GUILayout.Width(40));
				string newFilter = EditorGUILayout.TextField(m_ParticleNameFilter);
				if (newFilter != m_ParticleNameFilter)
				{
					m_ParticleNameFilter = newFilter;
					m_IsFilterActive = !string.IsNullOrEmpty(m_ParticleNameFilter);
				}

				if (GUILayout.Button(new GUIContent("초기화", "필터를 초기화합니다."), GUILayout.Width(60)))
				{
					m_ParticleNameFilter = "";
					m_IsFilterActive = false;
				}
				EditorGUILayout.EndHorizontal();

				EditorGUILayout.Space(5);

				// 파티클 상태 범례 표시
				EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
				EditorGUILayout.LabelField("상태:", GUILayout.Width(70));

				foreach (var stateIcon in m_StateIcons)
				{
					GUILayout.Label(stateIcon.Value, GUILayout.Width(15));
					EditorGUILayout.LabelField(GetStatusText(stateIcon.Key), GUILayout.Width(70));
				}

				EditorGUILayout.EndHorizontal();

				EditorGUILayout.Space(5);

				// 캐시된 파티클 목록이 없으면 새로 가져옴
				var activeParticles = m_CachedParticles;
				if (activeParticles == null || !Application.isPlaying)
				{
					// 기존 방식인 GetActiveParticles 대신 ParticleManager의 직접 메서드 사용
					if (particleManager.GetType().GetMethod("GetAllParticles",
						System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance) != null)
					{
						// GetAllParticles 메서드 호출을 시도
						activeParticles = particleManager.GetAllParticles();
					}
					else
					{
						// 이전 방식으로 fallback
						activeParticles = GetActiveParticles(particleManager);
					}
					m_CachedParticles = activeParticles;
				}

				if (activeParticles != null && activeParticles.Count > 0)
				{
					// 스크롤 시작 - 높이를 400으로 설정
					m_ScrollPosition = EditorGUILayout.BeginScrollView(m_ScrollPosition, GUILayout.Height(400));

					// 활성 파티클 목록을 필터링하여 표시
					var filteredParticles = activeParticles.Where(p =>
						p != null && (!m_IsFilterActive || (p.ParticleName != null && p.ParticleName.ToLower().Contains(m_ParticleNameFilter.ToLower())))).ToList();

					if (filteredParticles.Count > 0)
					{
						foreach (var particle in filteredParticles)
						{
							// 파티클 항목 표시
							DrawParticleItem(particle);
						}
					}
					else if (m_IsFilterActive)
					{
						EditorGUILayout.HelpBox($"'{m_ParticleNameFilter}' 검색어와 일치하는 파티클이 없습니다.", MessageType.Info);
					}
					else
					{
						EditorGUILayout.HelpBox("활성화된 파티클이 없습니다.", MessageType.Info);
					}

					EditorGUILayout.EndScrollView();
				}
				else
				{
					EditorGUILayout.HelpBox("현재 활성화된 파티클이 없습니다.", MessageType.Info);
				}

				EditorGUILayout.EndVertical();
			}

			EditorGUILayout.Space(5);

			// 제어 버튼 영역
			EditorGUILayout.BeginHorizontal();

			// 모든 파티클 중지 버튼
			if (GUILayout.Button(new GUIContent("모든 파티클 중지", "현재 활성화된 모든 파티클을 중지합니다."), GUILayout.Height(30)))
			{
				// 모든 파티클 중지
				particleManager.StopAllParticles();

				// 캐시 초기화
				m_CachedParticles = null;

				Repaint(); // UI 갱신
			}

			// 데이터 새로고침 버튼
			if (GUILayout.Button(new GUIContent("데이터 새로고침", "파티클 데이터를 다시 로드합니다."), GUILayout.Height(30)))
			{
				// Reflection을 사용하여 private DataInit 메서드 호출
				System.Reflection.MethodInfo method = typeof(ParticleManager).GetMethod("DataInit",
					System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
				if (method != null)
				{
					method.Invoke(particleManager, null);
					Debug.Log("ParticleManager: 파티클 데이터를 다시 로드했습니다.");
				}

				// 캐시 초기화
				m_CachedParticles = null;

				Repaint(); // UI 갱신
			}

			// 정리(CleanUP) 버튼
			if (GUILayout.Button(new GUIContent("수동 정리", "사용되지 않는 파티클을 정리합니다."), GUILayout.Height(30)))
			{
				// ForceCleanup 메서드 호출 시도 (새로 추가된 공개 메서드)
				System.Reflection.MethodInfo publicMethod = typeof(ParticleManager).GetMethod("ForceCleanup",
					System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);

				if (publicMethod != null)
				{
					publicMethod.Invoke(particleManager, null);
					Debug.Log("ParticleManager: 파티클을 수동으로 정리했습니다.");
				}
				else
				{
					// 이전 버전 호환성을 위한 fallback - private CleanUP 메서드 호출
					System.Reflection.MethodInfo privateMethod = typeof(ParticleManager).GetMethod("CleanUP",
						System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
					if (privateMethod != null)
					{
						privateMethod.Invoke(particleManager, null);
						Debug.Log("ParticleManager: 파티클을 수동으로 정리했습니다.");
					}
				}

				// 캐시 초기화
				m_CachedParticles = null;

				Repaint(); // UI 갱신
			}

			EditorGUILayout.EndHorizontal();

			// 직렬화 객체 변경사항 적용
			if (serializedObject.hasModifiedProperties)
			{
				serializedObject.ApplyModifiedProperties();
			}
		}

		/// <summary>
		/// ParticleManager에서 활성화된 파티클 목록을 가져옵니다.
		/// </summary>
		private List<TemporaryParticlePlayer> GetActiveParticles(ParticleManager manager)
		{
			// Reflection을 사용하여 private 필드 접근
			System.Reflection.FieldInfo field = typeof(ParticleManager).GetField("m_InstantiatedParticles",
				System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

			if (field != null)
			{
				var particles = field.GetValue(manager) as List<TemporaryParticlePlayer>;
				if (particles != null)
				{
					// null이 아닌 파티클만 필터링
					return particles.Where(p => p != null).ToList();
				}
			}

			return new List<TemporaryParticlePlayer>();
		}

		/// <summary>
		/// 개별 파티클 항목을 그립니다.
		/// </summary>
		/// <param name="particle">표시할 파티클 플레이어</param>
		private void DrawParticleItem(TemporaryParticlePlayer particle)
		{
			if (particle == null) return;

			// 파티클 항목 배경 (상태에 따라 색상 변경)
			var particleState = particle.GetParticleState();
			Color bgColor = GUI.backgroundColor;

			switch (particleState)
			{
				case ParticleState.Playing:
					GUI.backgroundColor = new Color(0.8f, 1.0f, 0.8f); // 연한 녹색
					break;
				case ParticleState.FadingOut:
					GUI.backgroundColor = new Color(1.0f, 0.95f, 0.8f); // 연한 노란색
					break;
				case ParticleState.Stopped:
					GUI.backgroundColor = new Color(0.8f, 0.8f, 0.8f); // 연한 회색
					break;
				case ParticleState.Error:
					GUI.backgroundColor = new Color(1.0f, 0.8f, 0.8f); // 연한 빨간색
					break;
			}

			EditorGUILayout.BeginVertical(EditorStyles.helpBox);
			GUI.backgroundColor = bgColor; // 색상 복원

			// 첫 번째 줄: 이름과 상태
			EditorGUILayout.BeginHorizontal();

			// 파티클 아이콘
			GUILayout.Label(m_ParticleIcon, GUILayout.Width(20), GUILayout.Height(20));

			// 파티클 이름
			string displayName = string.IsNullOrEmpty(particle.ParticleName) ? "(이름 없음)" : particle.ParticleName;
			EditorGUILayout.LabelField(displayName, EditorStyles.boldLabel);

			// 여백 추가
			GUILayout.FlexibleSpace();

			// 상태 아이콘 (툴팁 포함)
			if (m_StateIcons.TryGetValue(particleState, out GUIContent stateIcon))
			{
				GUILayout.Label(stateIcon, GUILayout.Width(20), GUILayout.Height(20));
			}

			EditorGUILayout.EndHorizontal();

			// 두 번째 줄: 파티클 정보
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField($"지속 시간: {particle.TotalDuration:F2}초", GUILayout.Width(120));
			EditorGUILayout.LabelField($"경과 시간: {particle.ElapsedTime:F2}초", GUILayout.Width(120));

			// 진행 상태 (바 형태로 표시)
			if (particle.IsPlaying)
			{
				float progress = particle.Progress;
				EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(GUILayout.Height(18)), progress, $"{progress * 100:F1}%");
			}
			else
			{
				EditorGUILayout.LabelField("진행 중지", GUILayout.Height(18));
			}

			EditorGUILayout.EndHorizontal();

			// 세 번째 줄: 파티클 제어 버튼
			EditorGUILayout.BeginHorizontal();

			// "선택" 대신 "이동" 버튼으로 변경
			if (GUILayout.Button(new GUIContent("이동", "이 파티클의 위치로 씬 뷰를 이동합니다."), GUILayout.Width(80)))
			{
				// 파티클 선택 (기존 기능 유지)
				Selection.activeGameObject = particle.gameObject;

				// 씬 뷰에서 해당 위치로 이동
				SceneView.lastActiveSceneView.Frame(
					new Bounds(particle.transform.position, Vector3.one * 3f),
					false // 즉시 이동
				);

				// 추가로 오브젝트 하이라이트
				EditorGUIUtility.PingObject(particle.gameObject);
			}

			// 파티클 중지
			if (particle.IsPlaying && GUILayout.Button("중지", GUILayout.Width(80)))
			{
				particle.Stop();
			}

			// 부드러운 중지
			if (particle.IsPlaying && GUILayout.Button("부드럽게 중지", GUILayout.Width(120)))
			{
				particle.SmoothStop();
			}

			// 스케일 페이드 아웃
			if (particle.IsPlaying && GUILayout.Button("스케일 페이드", GUILayout.Width(120)))
			{
				particle.SmoothStopWithScale();
			}

			EditorGUILayout.EndHorizontal();

			EditorGUILayout.EndVertical();

			// 항목 사이 공간
			EditorGUILayout.Space(2);
		}

		/// <summary>
		/// 파티클 상태에 따른 상태 텍스트를 반환합니다.
		/// </summary>
		private string GetStatusText(ParticleState state)
		{
			switch (state)
			{
				case ParticleState.Ready:
					return "준비됨";
				case ParticleState.Playing:
					return "재생 중";
				case ParticleState.FadingOut:
					return "페이드 아웃";
				case ParticleState.Stopped:
					return "중지됨";
				case ParticleState.Error:
					return "오류";
				default:
					return "알 수 없음";
			}
		}

		/// <summary>
		/// 파티클 상태에 따른 색상을 반환합니다.
		/// </summary>
		private Color GetStatusColor(ParticleState state)
		{
			switch (state)
			{
				case ParticleState.Ready:
					return new Color(0.5f, 0.5f, 1.0f); // 파란색
				case ParticleState.Playing:
					return new Color(0.0f, 0.8f, 0.0f); // 녹색
				case ParticleState.FadingOut:
					return new Color(1.0f, 0.8f, 0.0f); // 노란색
				case ParticleState.Stopped:
					return new Color(0.5f, 0.5f, 0.5f); // 회색
				case ParticleState.Error:
					return new Color(1.0f, 0.3f, 0.3f); // 빨간색
				default:
					return Color.white;
			}
		}

		#endregion
	}
}
