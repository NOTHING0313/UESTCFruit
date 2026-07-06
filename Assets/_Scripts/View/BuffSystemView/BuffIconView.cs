using BuffSystem;
using UnityEngine;
using UnityEngine.UI;

namespace View
{
    public sealed class BuffIconView : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private Text _stackText;
        [SerializeField] private Text _timeText;

        public void Bind(in BuffViewData data, BuffUIViewConfig config, float fixedDeltaTime)
        {
            if (_icon != null)
            {
                if (config != null && config.TryGet(data.ConfigId, out BuffUIViewConfig.Entry entry))
                {
                    _icon.sprite = entry.icon;
                    _icon.color = entry.color == default ? Color.white : entry.color;
                }
                else
                {
                    _icon.sprite = null;
                    _icon.color = Color.white;
                }
            }

            if (_stackText != null)
                _stackText.text = data.Stack > 1 ? data.Stack.ToString() : string.Empty;

            if (_timeText != null)
            {
                if (data.RemainingFrames < 0)
                {
                    _timeText.text = string.Empty;
                }
                else
                {
                    float seconds = data.RemainingFrames * fixedDeltaTime;
                    _timeText.text = seconds >= 10f
                        ? Mathf.CeilToInt(seconds).ToString()
                        : seconds.ToString("0.0");
                }
            }
        }
    }
}