using BuffSystem;
using BuffSystem.BuffInstance;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Test
{
    public class TestBuffGiver : MonoBehaviour
    {
        public BuffHandler handler;
        public int buffID;
        [Button("GiveBuff")]
        public void GiveBuff()
        {
            if (handler == null) return;
            handler.AddBuff<SimpleBuff>(buffID,BuffRuntimeDataFactory.Get(gameObject,handler.gameObject,1));
        }
        [Button("RemoveBuff")]
        public void RemoveBuff()
        {
            if (handler == null) return;
            handler.RemoveBuffStack(buffID, gameObject);
        }
    }
}
