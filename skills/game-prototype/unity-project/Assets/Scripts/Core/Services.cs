using System;
using System.Collections.Generic;
using UnityEngine;

namespace MelonS.GameProto.Core
{
    /// <summary>
    /// R6 - Lightweight ServiceLocator.  Replaces 5 `static Instance` singletons
    /// (GameClock / ResourceManager / AudioBank / WeatherController / ResearchManager)
    /// with a single typed lookup.
    ///
    /// Caller 호환: 각 singleton 의 `Instance` static property 는 유지하되,
    /// 내부적으로 Services.Get&lt;T&gt;() 를 호출함.  →caller 코드 무변경.
    ///
    /// 테스트성: Services.Register&lt;T&gt;(mockInstance) 로 PlayMode 자동 테스트
    /// (R7) 에서 fake / spy 주입 가능.
    ///
    /// Scene 전환 시: Awake 가 다시 Register 함.  명시적 Services.Clear() 는
    /// SceneLoader 가 필요 시 호출 (현재는 자동 register-on-Awake 만 신뢰).
    /// </summary>
    public static class Services
    {
        private static readonly Dictionary<Type, object> _registry = new Dictionary<Type, object>();

        public static void Register<T>(T service) where T : class
        {
            if (service == null) { _registry.Remove(typeof(T)); return; }
            _registry[typeof(T)] = service;
        }

        public static T Get<T>() where T : class
        {
            if (_registry.TryGetValue(typeof(T), out var s)) return s as T;
            return null;
        }

        public static bool Has<T>() where T : class => _registry.ContainsKey(typeof(T));

        public static void Unregister<T>() where T : class => _registry.Remove(typeof(T));

        /// <summary>Test 용 - 모든 등록 비움.</summary>
        public static void Clear() => _registry.Clear();
    }
}
