using System;
using UnityEngine;
[Serializable]
public struct HeadInfo
{
    public int ID;
    public string Name;
    public string Description;
    public Sprite Icon;
    public override string ToString()
    {
        return $"\nHeadInfo:" +
            $"\nID: {ID}\n" +
            $"Name: {Name}\n";
    }
}