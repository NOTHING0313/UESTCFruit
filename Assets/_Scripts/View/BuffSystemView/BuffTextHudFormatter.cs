using System.Collections.Generic;
using System.Text;

namespace View
{
    /// <summary>
    /// Formats Buff view models for a minimal text HUD.
    /// </summary>
    public sealed class BuffTextHudFormatter
    {
        private const string EmptyText = "No Buffs";

        public string Format(IReadOnlyList<BuffViewModel> buffs)
        {
            if (buffs == null || buffs.Count == 0)
                return EmptyText;

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Buffs:");

            for (int i = 0; i < buffs.Count; i++)
            {
                BuffViewModel buff = buffs[i];
                string debugName = string.IsNullOrEmpty(buff.DebugName) ? $"Buff {buff.ConfigId}" : buff.DebugName;
                string effectIdText = string.IsNullOrEmpty(buff.EffectIdText) ? "N/A" : buff.EffectIdText;

                builder
                    .Append("- [")
                    .Append(buff.ConfigId)
                    .Append("] ")
                    .Append(debugName)
                    .Append(" | Stack: ")
                    .Append(buff.Stack)
                    .Append(" | Remain: ")
                    .Append(buff.RemainingFrames)
                    .Append(" | Source: ")
                    .Append(buff.SourceEntity)
                    .Append(" | Effect: ")
                    .Append(effectIdText);

                if (i < buffs.Count - 1)
                    builder.AppendLine();
            }

            return builder.ToString();
        }
    }
}
