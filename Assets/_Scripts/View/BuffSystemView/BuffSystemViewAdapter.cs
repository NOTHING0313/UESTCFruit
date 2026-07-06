using BuffSystem;
using ECSFrameWork;
using System;
using System.Collections.Generic;

namespace View
{
    /// <summary>
    /// Optional View-only resolver for presentation metadata.
    /// </summary>
    public interface IBuffViewDefinitionResolver
    {
        bool TryResolve(int configId, out BuffViewDefinitionViewData viewData);
    }

    /// <summary>
    /// Minimal Buff definition data that View may display.
    /// </summary>
    public readonly struct BuffViewDefinitionViewData
    {
        public readonly int ConfigId;
        public readonly int EffectId;
        public readonly string DebugName;

        public BuffViewDefinitionViewData(int configId, int effectId, string debugName)
        {
            ConfigId = configId;
            EffectId = effectId;
            DebugName = debugName ?? string.Empty;
        }
    }

    /// <summary>
    /// Builds View-facing Buff models from IBuffSystem public query data.
    /// </summary>
    public sealed class BuffSystemViewAdapter
    {
        private readonly IBuffViewDefinitionResolver _definitionResolver;

        public BuffSystemViewAdapter(IBuffViewDefinitionResolver definitionResolver = null)
        {
            _definitionResolver = definitionResolver;
        }

        public IReadOnlyList<BuffViewModel> BuildViewModels(IBuffSystem buffSystem, Entity ownerEntity)
        {
            if (buffSystem == null || !ownerEntity.IsValid)
                return Array.Empty<BuffViewModel>();

            IReadOnlyList<BuffViewData> source = buffSystem.GetBuffs(ownerEntity);
            if (source == null || source.Count == 0)
                return Array.Empty<BuffViewModel>();

            List<BuffViewModel> result = new List<BuffViewModel>(source.Count);
            for (int i = 0; i < source.Count; i++)
                result.Add(ToViewModel(source[i]));

            return result;
        }

        public IReadOnlyList<BuffViewModel> BuildViewModels(IBuffSystem buffSystem, int ownerEntity)
        {
            Entity entity = ownerEntity >= 0 ? new Entity(ownerEntity, 1) : Entity.Invalid;
            return BuildViewModels(buffSystem, entity);
        }

        private BuffViewModel ToViewModel(in BuffViewData data)
        {
            string effectIdText = "N/A";
            string debugName = $"Buff {data.ConfigId}";

            if (_definitionResolver != null &&
                _definitionResolver.TryResolve(data.ConfigId, out BuffViewDefinitionViewData definition))
            {
                if (definition.EffectId > 0)
                    effectIdText = definition.EffectId.ToString();

                if (!string.IsNullOrWhiteSpace(definition.DebugName))
                    debugName = definition.DebugName;
            }

            return new BuffViewModel(
                data.ConfigId,
                data.Stack,
                data.RemainingFrames,
                data.Source.ID,
                effectIdText,
                debugName);
        }
    }
}
