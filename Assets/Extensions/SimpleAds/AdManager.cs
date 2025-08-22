using System;
using System.Collections.Generic;
using System.Linq;
using Assets.SimpleAds.Adapters;
using UnityEngine;
using Logger = Assets.SimpleAds.Service.Logger;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
#endif

#if USE_REMOTE_WATERFALL
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.RemoteConfig;
#endif

namespace Assets.SimpleAds
{
    /// <summary>
    /// 광고 관리를 담당하는 클래스입니다.
    /// </summary>
    public class AdManager : Logger
    {
        [Header("Networks")]
        [Tooltip("Unity Ads 사용 여부")]
        public bool UseUnityAds;

        [Tooltip("AdMob 사용 여부")]
        public bool UseAdMob;

        [Tooltip("IronSource 사용 여부")]
        public bool UseIronSource;

        [Tooltip("Vungle 사용 여부")]
        public bool UseVungle;

        [Tooltip("Yandex Ads 사용 여부")]
        public bool UseYandexAds;

        [Tooltip("광고 어댑터들의 리스트")]
        public List<AdAdapter> Adapters;

        [Header("Settings")]
        [Tooltip("자동 초기화 여부")]
        public bool AutoInitialize = true;

        [Tooltip("원격 워터폴 사용 여부")]
        public bool UseRemoteWaterfall;

        [Tooltip("원격 설정 가져오기 여부")]
        public bool FetchRemoteConfig = true;

        [Header("Waterfalls")]
        [Tooltip("전면 광고 워터폴")]
        public List<string> WaterfallInterstitial = new List<string> { "AdMob", "IronSource", "UnityAds", "Vungle", "YandexAds" };

        [Tooltip("리워드 광고 워터폴")]
        public List<string> WaterfallRewarded = new List<string> { "AdMob", "IronSource", "UnityAds", "Vungle", "YandexAds" };

        [Tooltip("배너 광고 워터폴")]
        public List<string> WaterfallBanner = new List<string> { "AdMob", "IronSource", "UnityAds", "Vungle", "YandexAds" };

        public static AdManager Instance;

        private AdAdapter _bannerAdapter;

        private static bool _fetched;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            if (AutoInitialize)
            {
                Initialize();
            }

#if USE_REMOTE_WATERFALL
            HandleFetchCompleted();

            if (FetchRemoteConfig)
            {
                FetchConfigs();
            }
#endif
        }

        /// <summary>
        /// 광고 어댑터들을 초기화합니다.
        /// </summary>
        public void Initialize()
        {
            Event("Initialize");
            Adapters.ForEach(i => i.Initialize());
        }

        /// <summary>
        /// 전면 광고 준비 여부를 확인합니다.
        /// </summary>
        /// <returns>전면 광고가 준비되었는지 여부</returns>
        public bool IsReadyInterstitial()
        {
            return Adapters.Any(i => i.IsReadyInterstitial());
        }

        /// <summary>
        /// 리워드 광고 준비 여부를 확인합니다.
        /// </summary>
        /// <returns>리워드 광고가 준비되었는지 여부</returns>
        public bool IsReadyRewarded()
        {
            return Adapters.Any(i => i.IsReadyRewarded());
        }

        /// <summary>
        /// 배너 광고 준비 여부를 확인합니다.
        /// </summary>
        /// <returns>배너 광고가 준비되었는지 여부</returns>
        public bool IsReadyBanner()
        {
            return Adapters.Any(i => i.IsReadyBanner());
        }

        /// <summary>
        /// 전면 광고를 표시합니다.
        /// </summary>
        public void ShowInterstitial()
        {
            var adapter = OrderAdapters(WaterfallInterstitial).FirstOrDefault(i => i.IsReadyInterstitial());

            if (adapter == null)
            {
                Debug.LogWarning("Interstitial not ready."); // 전면 광고 준비되지 않음
            }
            else
            {
                Event("ShowInterstitial", "Provider", adapter.ProviderName);
                adapter.ShowInterstitial();
            }
        }

        /// <summary>
        /// 리워드 광고를 표시합니다.
        /// </summary>
        /// <param name="callback">광고 시청 완료 후 실행될 콜백 함수</param>
        public void ShowRewarded(Action callback)
        {
            var adapter = OrderAdapters(WaterfallRewarded).FirstOrDefault(i => i.IsReadyRewarded());

            if (adapter == null)
            {
                Debug.LogWarning("Rewarded not ready."); // 리워드 광고 준비되지 않음
            }
            else
            {
                Event("AdManager.ShowRewarded", "Provider", adapter.ProviderName);
                adapter.ShowRewarded((providerName, reward, amount) => callback?.Invoke());
            }
        }

        /// <summary>
        /// 리워드 광고를 표시합니다.
        /// </summary>
        /// <param name="callback">광고 시청 완료 후 실행될 콜백 함수 (광고 제공자, 리워드 종류, 리워드 양 포함)</param>
        public void ShowRewarded(Action<string, string, float> callback)
        {
            var adapter = OrderAdapters(WaterfallRewarded).FirstOrDefault(i => i.IsReadyRewarded());

            if (adapter == null)
            {
                Debug.LogWarning("Rewarded not ready."); // 리워드 광고 준비되지 않음
            }
            else
            {
                Event("AdManager.ShowRewarded", "Provider", adapter.ProviderName);
                adapter.ShowRewarded(callback);
            }
        }

        /// <summary>
        /// 배너 광고를 표시합니다.
        /// </summary>
        public void ShowBanner()
        {
            var adapter = OrderAdapters(WaterfallBanner).FirstOrDefault(i => i.IsReadyBanner());

            if (_bannerAdapter != null && _bannerAdapter != adapter)
            {
                _bannerAdapter.HideBanner(); // 이전 배너 숨기기
            }

            _bannerAdapter = adapter;

            if (_bannerAdapter == null)
            {
                Debug.LogWarning("Banner not ready."); // 배너 광고 준비되지 않음
            }
            else
            {
                Event("ShowBanner", "Provider", _bannerAdapter.ProviderName);
                _bannerAdapter.ShowBanner();
            }
        }

        /// <summary>
        /// 배너 광고를 숨깁니다.
        /// </summary>
        public void HideBanner()
        {
            _bannerAdapter?.HideBanner();
        }

        private IEnumerable<AdAdapter> OrderAdapters(List<string> waterfall)
        {
            return Adapters.OrderBy(i => waterfall.Contains(i.ProviderName) ? waterfall.IndexOf(i.ProviderName) : 999);
        }

#if USE_REMOTE_WATERFALL
        private void HandleFetchCompleted()
        {
            RemoteConfigService.Instance.FetchCompleted += response =>
            {
                if (response.status == ConfigRequestStatus.Success)
                {
                    Debug.Log($"<color=yellow>RemoteConfigService.Instance.FetchCompleted ({response.requestOrigin})</color>");

                    // 전면 광고 워터폴 업데이트
                    if (RemoteConfigService.Instance.appConfig.HasKey("AdManager.Waterfall.Interstitial"))
                    {
                        var value = RemoteConfigService.Instance.appConfig.GetString("AdManager.Waterfall.Interstitial");
                        Event("RemoteConfig.FetchCompleted", "WaterfallInterstitial", value);
                        WaterfallInterstitial = value.Split(',').ToList();
                    }

                    // 리워드 광고 워터폴 업데이트
                    if (RemoteConfigService.Instance.appConfig.HasKey("AdManager.Waterfall.Rewarded"))
                    {
                        var value = RemoteConfigService.Instance.appConfig.GetString("AdManager.Waterfall.Rewarded");
                        Event("RemoteConfig.FetchCompleted", "WaterfallRewarded", value);
                        WaterfallRewarded = value.Split(',').ToList();
                    }

                    // 배너 광고 워터폴 업데이트
                    if (RemoteConfigService.Instance.appConfig.HasKey("AdManager.Waterfall.Banner"))
                    {
                        var value = RemoteConfigService.Instance.appConfig.GetString("AdManager.Waterfall.Banner");
                        Event("RemoteConfig.FetchCompleted", "WaterfallBanner", value);
                        WaterfallBanner = value.Split(',').ToList();
                    }
                }
            };
        }

        private static async void FetchConfigs()
        {
            if (_fetched) return;

            try
            {
                await UnityServices.InitializeAsync(); // Unity Services 초기화

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync(); // 익명 로그인
                }

                RemoteConfigService.Instance.FetchConfigs(new UserAttributes(), new AppAttributes()); // 원격 설정 가져오기

                _fetched = true; // 가져오기 완료 표시
            }
            catch (Exception e)
            {
                Debug.LogWarning(e.Message); // 오류 메시지 출력
            }
        }

        private struct UserAttributes
        {
        }

        private struct AppAttributes
        {
        }
#endif

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying || !gameObject.activeSelf) return;

            var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup))
                .Split(';')
                .ToList();
            var copy = defines.ToList();

            // 기존의 DEFINE 심볼 제거
            foreach (var define in new[] { "USE_REMOTE_WATERFALL", "USE_UNITYADS", "USE_ADMOB", "USE_IRONSOURCE", "USE_VUNGLE", "USE_YANDEXADS" })
            {
                if (defines.Contains(define)) defines.Remove(define);
            }

            // 설정에 따라 DEFINE 심볼 추가
            if (UseRemoteWaterfall) defines.Add("USE_REMOTE_WATERFALL");
            if (UseUnityAds) defines.Add("USE_UNITYADS");
            if (UseAdMob) defines.Add("USE_ADMOB");
            if (UseIronSource) defines.Add("USE_IRONSOURCE");
            if (UseVungle) defines.Add("USE_VUNGLE");
            if (UseYandexAds) defines.Add("USE_YANDEXADS");

            // DEFINE 심볼이 변경되었으면 업데이트
            if (!defines.OrderBy(i => i).SequenceEqual(copy.OrderBy(i => i)))
            {
                // 새로운 API 사용
                PlayerSettings.SetScriptingDefineSymbols(
                    NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup),
                    string.Join(";", defines));
            }

            UpdateAdapters(); // 어댑터 업데이트
        }

        private void UpdateAdapters()
        {
            Adapters.Clear();

            if (UseUnityAds) Adapters.Add(GetComponent<AdapterUnityAds>() ?? gameObject.AddComponent<AdapterUnityAds>());
            if (UseAdMob) Adapters.Add(GetComponent<AdapterAdMob>() ?? gameObject.AddComponent<AdapterAdMob>());
            if (UseIronSource) Adapters.Add(GetComponent<AdapterIronSource>() ?? gameObject.AddComponent<AdapterIronSource>());
            if (UseVungle) Adapters.Add(GetComponent<AdapterVungle>() ?? gameObject.AddComponent<AdapterVungle>());
            if (UseYandexAds) Adapters.Add(GetComponent<AdapterYandexAds>() ?? gameObject.AddComponent<AdapterYandexAds>());
        }
#endif
    }
}
