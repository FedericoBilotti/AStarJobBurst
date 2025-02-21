using UnityEngine;

namespace Utilities
{
    public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;

        public static T Instance
        {
            get
            {
                if (_instance) return _instance;
                
                _instance = FindFirstObjectByType<T>();

                if (_instance) return _instance;
                var singleton = new GameObject().AddComponent<T>();
                singleton.name = typeof(T).ToString();

                return _instance;
            }
        }

        private void Awake()
        {
            _instance = this as T;
            
            InitializeSingleton();
        }

        protected virtual void InitializeSingleton()
        {
            
        }
    }
}