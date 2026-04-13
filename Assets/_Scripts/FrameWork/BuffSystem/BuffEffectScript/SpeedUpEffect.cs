using Sirenix.OdinInspector;
using Test;
using UnityEngine;

namespace BuffSystem.Effect
{
    [CreateAssetMenu(menuName = "BuffSystem/BuffEffect/SpeedUpEffect", fileName = "SpeedUpEffect")]
    public sealed class SpeedUpEffect : BuffEffect
    {
        [SerializeField, LabelText("EffectID")] private int effectKey = 0;
        [SerializeField, LabelText("改变比率")] private float rate = 0;
        [SerializeField, LabelText("改变数值")] private float num = 0;
        public override int EffectKey => effectKey;
        public override void OnStackChanged(in BuffContext ctx, int delta)
        {
            ctx.Handler.GetComponent<TestBuffChractor>().Change(rate * delta, num * delta);
            Debug.Log(ctx.Handler.GetComponent<TestBuffChractor>().Speed.ValueRate);
        }
    }
}
