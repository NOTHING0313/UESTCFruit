using Sirenix.OdinInspector;
using UnityEngine;

namespace BuffSystem
{
    [CreateAssetMenu(menuName = "BuffSystem/Effects/CompositeEffect", fileName = "CompositeEffect")]
    public sealed class CompositeEffect : BuffEffect
    {
        [SerializeField, LabelText("Effect¼¯ºÏ"),Searchable] private BuffEffect[] effects;
        [SerializeField, LabelText("EffectID")] private int effectKey = 0;
        public override int EffectKey => effectKey;

        public override void OnApply(in BuffContext ctx)
        {
            foreach (BuffEffect effect in effects) effect?.OnApply(in ctx);
        }
        public override void OnRefresh(in BuffContext ctx)
        {
            foreach (BuffEffect effect in effects) effect?.OnRefresh(in ctx);
        }
        public override void OnStackChanged(in BuffContext ctx, int delta)
        {
            foreach (BuffEffect effect in effects) effect?.OnStackChanged(in ctx, delta);
        }
        public override void OnTick(in BuffContext ctx)
        {
            foreach (BuffEffect effect in effects) effect?.OnTick(in ctx);
        }
        public override void OnEvent(in BuffContext ctx)
        {
            foreach (BuffEffect effect in effects) effect?.OnEvent(in ctx);
        }
        public override void OnRemove(in BuffContext ctx)
        {
            foreach (BuffEffect effect in effects) effect?.OnRemove(in ctx);
        }
    }
}