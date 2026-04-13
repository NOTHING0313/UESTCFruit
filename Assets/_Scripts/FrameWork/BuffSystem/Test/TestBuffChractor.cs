using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace Test
{
    [SerializeField]
    public struct AttritudeHandle
    {
        private float _value;
        private float _maxValue;
        private float _valueRate;
        private float _valueNum;
        private bool _isBiggerThanErzo;
        public float ValueRate => _valueRate;
        public float ValueNum => _valueNum;
        public float Value
        {
            get
            {
                float value = _value * _valueRate + _valueNum;
                if (_isBiggerThanErzo && value < 0)
                    value = 0;
                if (value > _maxValue)
                    value = _maxValue;
                return value;
            }
        }
        public AttritudeHandle(float value, float maxValue, float valueRate = 1, float valueNum = 0, bool isBiggerThanErzo = true) => (_value, _maxValue, _valueRate, _valueNum, _isBiggerThanErzo) = (value, maxValue, valueRate, valueNum, isBiggerThanErzo);
        public void ChangeRate(float value)
        {
            float valueRate = _valueRate;
            valueRate += value;
            if (valueRate < 0)
                valueRate = 0;
            _valueRate = valueRate;
        }
        public void ChangeNum(float value) => _valueNum += value;
        public void Change(float rate, float num)
        {
            ChangeRate(rate);
            ChangeNum(num);
        }
    }
    public class TestBuffChractor : MonoBehaviour
    {
        private AttritudeHandle _speed = new(5, 10);
        public void Change(float rate, float num) => _speed.Change(rate, num);
        [ShowInInspector]public float SpeedNum => _speed.Value;
        public AttritudeHandle Speed => _speed;
    }
}
