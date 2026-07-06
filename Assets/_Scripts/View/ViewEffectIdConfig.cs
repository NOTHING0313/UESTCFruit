using UnityEngine;

namespace View
{
    [CreateAssetMenu(menuName = "Simulation/View Effect Id Config")]
    public sealed class ViewEffectIdConfig : ScriptableObject
    {
        [SerializeField] private int _damageEffectId = 100;
        [SerializeField] private int _deadEffectId = 200;

        public int DamageEffectId => _damageEffectId;
        public int DeadEffectId => _deadEffectId;
    }
}