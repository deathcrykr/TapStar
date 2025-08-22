using System;
using UnityEngine;

#if (UNITY_ANDROID || UNITY_IOS) && USE_YANDEXADS

using YandexMobileAds;
using YandexMobileAds.Base;

#endif

namespace Assets.SimpleAds.Adapters
{
    public class AdapterYandexAds : AdAdapter
    {
        #if (UNITY_ANDROID || UNITY_IOS) && USE_YANDEXADS
		
		public AdPosition BannerPosition = AdPosition.BottomCenter;

        public override string ProviderName => "YandexAds";

        public Interstitial Interstitial;
        public RewardedAd RewardedAd;
        public Banner Banner;
        
        public static bool InterstitialLoaded { get; private set; }
        public static bool RewardedAdLoaded { get; private set; }
        public static bool BannerLoaded { get; private set; }

        public static AdapterYandexAds Instance;

        public void Awake()
        {
            Instance = this;
        }

        public override void Initialize()
        {
            if (Initialized) return;

            Event("Initialize");

            if (Placements.LoadInterstitial && Placements.Interstitial != "") RequestInterstitial();
            if (Placements.LoadRewarded && Placements.Rewarded != "") RequestRewardedAd();
            if (Placements.LoadBanner && Placements.Banner != "") RequestBanner();

            Initialized = true;
        }

        public override bool IsReadyInterstitial()
        {
            return Interstitial?.IsLoaded() == true;
        }

        public override bool IsReadyRewarded()
        {
            return RewardedAd?.IsLoaded() == true;
        }

        public override bool IsReadyBanner()
        {
            return BannerLoaded;
        }

        public override void ShowInterstitial()
        {
            Event("ShowInterstitial");
            Interstitial.Show();
        }

        public override void ShowRewarded(Action<string, string, float> rewardAction)
        {
            Event("ShowRewarded");
            RewardAction = rewardAction;
            RewardedAd.Show();
        }

        public override void ShowBanner()
        {
            Event("ShowBanner");
            Banner.Show();
        }

        public override void HideBanner()
        {
            Event("HideBanner");
            Banner.Hide();
            Banner.Destroy();
            RequestBanner();
        }

        public void OnDestroy()
        {
            Banner?.Destroy();
        }

        public void RequestInterstitial()
        {
            if (Interstitial == null)
            {
                Interstitial = new Interstitial(Placements.Interstitial);
                Interstitial.OnInterstitialLoaded += (sender, args) => { Event("OnInterstitialLoaded"); InterstitialLoaded = true; };
                Interstitial.OnInterstitialFailedToLoad += (sender, args) => { Event("OnInterstitialFailedToLoad", "Error", args.Message); };
                Interstitial.OnReturnedToApplication += (sender, args) => { Event("OnReturnedToApplication"); };
                Interstitial.OnLeftApplication += (sender, args) => { Event("OnLeftApplication"); };
                Interstitial.OnAdClicked += (sender, args) => { Event("OnAdClicked"); };
                Interstitial.OnInterstitialShown += (sender, args) => { Event("OnInterstitialShown"); RequestInterstitial(); };
                Interstitial.OnInterstitialDismissed += (sender, args) => { Event("OnInterstitialDismissed"); };
                Interstitial.OnImpression += (sender, impression) => { Event("OnImpression"); };
                Interstitial.OnInterstitialFailedToShow += (sender, args) => { Event("OnInterstitialFailedToShow"); };
            }

            var request = new AdRequest.Builder().Build();

            Interstitial.LoadAd(request);
        }

        public void RequestRewardedAd()
        {
            if (RewardedAd == null)
            {
                RewardedAd = new RewardedAd(Placements.Rewarded);
                RewardedAd.OnRewardedAdLoaded += (sender, args) => { Event("OnRewardedAdLoaded"); RewardedAdLoaded = true; };
                RewardedAd.OnRewardedAdFailedToLoad += (sender, args) => { Event("OnRewardedAdFailedToLoad", "Error", args.Message); };
                RewardedAd.OnReturnedToApplication += (sender, args) => { Event("OnReturnedToApplication"); };
                RewardedAd.OnLeftApplication += (sender, args) => { Event("OnLeftApplication"); };
                RewardedAd.OnAdClicked += (sender, args) => { Event("OnAdClicked"); };
                RewardedAd.OnRewardedAdShown += (sender, args) => { Event("OnRewardedAdShown"); RequestRewardedAd(); };
                RewardedAd.OnRewardedAdDismissed += (sender, args) => { Event("OnRewardedAdDismissed"); };
                RewardedAd.OnImpression += (sender, impression) => { Event("OnImpression"); };
                RewardedAd.OnRewarded += (sender, reward) => { Event("OnRewarded ", "Reward", reward.type, "Amount", reward.amount); GiveReward(reward.type, reward.amount); };
                RewardedAd.OnRewardedAdFailedToShow += (sender, args) => { Event("OnRewardedAdFailedToShow", "Error", args.Message); };
            }

            var request = new AdRequest.Builder().Build();

            RewardedAd.LoadAd(request);
        }

        public void RequestBanner()
        {
            var size = AdSize.FlexibleSize(ScreenUtils.ConvertPixelsToDp((int) Screen.safeArea.width), 100);

            Banner = new Banner(Placements.Banner, size, BannerPosition);
            Banner.OnAdLoaded += (sender, args) => { Event("OnAdLoaded"); BannerLoaded = true; };
            Banner.OnAdFailedToLoad += (sender, args) => { Event("OnAdFailedToLoad", "Error", args.Message); };
            Banner.OnReturnedToApplication += (sender, args) => { Event("OnReturnedToApplication"); };
            Banner.OnLeftApplication += (sender, args) => { Event("OnLeftApplication"); };
            Banner.OnAdClicked += (sender, args) => { Event("OnAdClicked"); };
            Banner.OnImpression += (sender, impression) => { Event("OnImpression"); };

            var request = new AdRequest.Builder().Build();

            Banner.LoadAd(request);
            Banner.Hide();
        }

        #endif
    }
}