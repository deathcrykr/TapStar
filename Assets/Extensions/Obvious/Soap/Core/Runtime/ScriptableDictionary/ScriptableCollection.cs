using System;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Obvious.Soap
{
    public abstract class ScriptableCollection : ScriptableBase, IReset
    {
        [Tooltip(
            "Clear the collection when:\n" +
            " Scene Loaded : when a scene is loaded.\n" +
            " Application Start : Once, when the application starts. Modifications persist between scenes")]
        [SerializeField] protected ResetType _resetOn = ResetType.SceneLoaded;

        [HideInInspector] public Action Modified;
        public event Action OnCleared;
        public abstract int Count { get; }
        public abstract bool CanBeSerialized();
        private ResetType _lastResetType;

        protected virtual void Awake()
        {
            hideFlags = HideFlags.DontUnloadUnusedAsset;
        }

        protected virtual void OnEnable()
        {
#if UNITY_EDITOR
            _lastResetType = _resetOn; 
#endif
            if (_resetOn == ResetType.None)
                return;

            Clear();
            RegisterResetHooks(_resetOn);
        }

        protected virtual void OnDisable()
        {
            UnregisterResetHooks(_resetOn);
        }

        private void RegisterResetHooks(ResetType resetType)
        {
            if (resetType == ResetType.SceneLoaded)
            {
                SceneManager.sceneLoaded += OnSceneLoaded;
            }
#if UNITY_EDITOR
            if (resetType == ResetType.ApplicationStarts || resetType == ResetType.SceneLoaded)
            {
                EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            }
#endif
        }

        private void UnregisterResetHooks(ResetType resetType)
        {
            if (resetType == ResetType.SceneLoaded)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
            }
#if UNITY_EDITOR
            if (resetType == ResetType.ApplicationStarts || resetType == ResetType.SceneLoaded)
            {
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            }
#endif
        }

        protected virtual void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single)
            {
                Clear();
            }
        }

        public virtual void Clear()
        {
            OnCleared?.Invoke();
            Modified?.Invoke();
        }

        internal override void Reset()
        {
            base.Reset();
            _resetOn = ResetType.SceneLoaded;
            Clear();
        }

        public void ResetValue() => Clear();

#if UNITY_EDITOR
        public virtual void OnPlayModeStateChanged(PlayModeStateChange playModeStateChange)
        {
            if (playModeStateChange == PlayModeStateChange.EnteredEditMode ||
                playModeStateChange == PlayModeStateChange.ExitingEditMode)
                Clear();
        }

        private void OnValidate()
        {
            if (_lastResetType != _resetOn)
            {
                UnregisterResetHooks(_lastResetType);
                RegisterResetHooks(_resetOn);
                _lastResetType = _resetOn;
            }
        }
#endif
    }
}