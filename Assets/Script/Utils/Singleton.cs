using UnityEngine;

/// <summary>
/// Singleton 클래스를 상속하여 특정 클래스의 인스턴스가 하나만 존재하도록 보장합니다.
/// </summary>
/// <typeparam name="T">MonoBehaviour를 상속하는 클래스</typeparam>
public class Singleton<T> : MonoBehaviour
	where T : MonoBehaviour
{
	// 유니크한 인스턴스 보관
	private static volatile T _instance;

	[Tooltip("씬 전환 시 오브젝트 파괴 방지 여부입니다.")]
	[SerializeField] private bool isDontDestroy = false;

	/// <summary>
	/// 싱글톤의 내부 초기화 여부 (필요 시 DataInit 등에서 사용)
	/// </summary>
	protected bool isInit = false;

	/// <summary>
	/// 어플리케이션 종료(또는 싱글톤 파괴) 시점에 true로 설정
	/// </summary>
	private static volatile bool m_ShuttingDown = false;

	/// <summary>
	/// 멀티 스레드 환경에서 Instance 프로퍼티 동기화용 Lock 객체
	/// </summary>
	private static readonly object m_Lock = new object();

	/// <summary>
	/// Singleton 인스턴스를 반환합니다.
	/// </summary>
	public static T Instance
	{
		get
		{
			if (!Application.isPlaying)
				return null;

			if (m_ShuttingDown)
			{
				// 어플리케이션 종료 등으로 싱글톤 사용이 불가할 때
				return _instance;
			}

			lock (m_Lock)
			{
				// 이미 인스턴스가 존재하면 반환
				if (_instance != null)
					return _instance;

				// 씬 내에서 먼저 인스턴스 검색
				// (Unity 2023+ : FindFirstObjectByType,
				//  그 이하 버전 : FindObjectOfType<T>() 또는 FindObjectsOfType<T>())
				_instance = (T)FindFirstObjectByType(typeof(T));
				if (_instance == null && Application.isPlaying)
				{
					// 없는 경우, 새로 GameObject를 만들어 붙임
					GameObject obj = new GameObject(typeof(T).Name + "_AutoCreate");
					_instance = obj.AddComponent<T>();

					// 인스펙터 설정 적용 (isDontDestroy==true라면 파괴 방지)
					var singletonComponent = _instance as Singleton<T>;
					if (singletonComponent != null && singletonComponent.isDontDestroy)
					{
						DontDestroyOnLoad(obj);
					}
				}
				return _instance;
			}
		}
	}

	/// <summary>
	/// 오브젝트가 초기화될 때 호출되는 메서드입니다.
	/// </summary>
	protected virtual void Awake()
	{
		if (!Application.isPlaying)
			return;

		// 이미 인스턴스가 존재하지만, 그게 자신이 아니라면 중복 → 파괴
		if (_instance != null && _instance != this)
		{
			Destroy(gameObject);
			return;
		}

		// 인스턴스가 없으면 이 오브젝트를 싱글톤으로 설정
		_instance = this as T;

		// DontDestroyOnLoad 설정 적용
		if (isDontDestroy)
		{
			// 이미 DontDestroyOnLoad에 있는지 체크
			// (gameObject.scene.name이 "DontDestroyOnLoad"면 이미 설정됨)
			if (transform.parent == null && gameObject.scene.name != "DontDestroyOnLoad")
			{
				DontDestroyOnLoad(gameObject);
			}
		}

		// 싱글톤 데이터 초기화
		if (!isInit)
		{
			DataInit();
			isInit = true;
		}

		// 어플리케이션/씬 전환 시점에 다시 사용 가능하도록
		m_ShuttingDown = false;
	}

	/// <summary>
	/// 필요한 경우 초기화 작업 (Instance 프로퍼티 사용 전 보장).
	/// </summary>
	protected virtual void DataInit()
	{
		// override 해서 원하는 초기화
	}

	/// <summary>
	/// 인스턴스가 파괴될 때 static 인스턴스를 null로 설정하거나,
	/// m_ShuttingDown 처리를 수행합니다.
	/// </summary>
	protected virtual void OnDestroy()
	{
		// 자신이 싱글톤 인스턴스라면 shuttingDown 처리
		if (_instance == this)
		{
			m_ShuttingDown = true;

			// _instance = null;
			// (명시적으로 null로 설정할지 여부는 상황에 따라 결정)
		}
	}

	/// <summary>
	/// 어플리케이션이 종료될 때 호출됩니다.
	/// </summary>
	protected virtual void OnApplicationQuit()
	{
		if (_instance == this)
		{
			m_ShuttingDown = true;

			// _instance = null;
		}
	}
}
