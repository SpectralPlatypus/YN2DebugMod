using System;
using System.Collections.Generic;
using UnityEngine;

namespace DebugMod
{
    class GameObjectCache<T> where T : UnityEngine.Object
    {
        Dictionary<int, GameObject> objectCache;

        public GameObjectCache(int capacity)
        {
            objectCache = new Dictionary<int, GameObject>(capacity);
        }

        public GameObjectCache() : this(5) { }

        public void ClearCache() => objectCache.Clear();

        public void UpdateCache(Func<T, GameObject> createFunc, bool state)
        {
            foreach(var t in UnityEngine.Object.FindObjectsOfType<T>())
            {
                int instId = t.GetInstanceID();
                if(!objectCache.ContainsKey(instId))
                {
                    var obj = createFunc(t);
                    if (!obj) continue;
                    objectCache.Add(instId, obj);
                }
                objectCache[instId].SetActive(state);
            }
        }

        public void ToggleAllObjects(bool state)
        {
            foreach(var pair in objectCache)
            {
                pair.Value.SetActive(state);
            }
        }
    }
}
