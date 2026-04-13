using UnityEngine;
namespace BuffSystem.Event
{
    public readonly struct AttackEvent : IGameEvent
    {
        public readonly GameObject Attacker;
        public readonly GameObject Target;
        public readonly int Damage;
        public AttackEvent(GameObject attacker, GameObject target, int damage)
            => (Attacker, Target, Damage) = (attacker, target, damage);
    }
}
