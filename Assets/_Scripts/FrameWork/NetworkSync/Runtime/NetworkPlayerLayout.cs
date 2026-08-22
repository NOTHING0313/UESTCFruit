using System;
using UnityEngine;

namespace FrameWork.NetworkSync
{
    /// <summary>根据 PlayerID 生成与本地玩家身份无关的确定性初始位置。</summary>
    public static class NetworkPlayerLayout
    {
        public static Vector3 GetSpawnPosition(int playerID,int playerCount,float spacing)
            =>new(GetSpawnX(playerID,playerCount,spacing),0f,0f);

        public static float GetSpawnX(int playerID,int playerCount,float spacing)
        {
            if(playerCount<=0) throw new ArgumentOutOfRangeException(nameof(playerCount));
            if(playerID<=0||playerID>playerCount) throw new ArgumentOutOfRangeException(nameof(playerID));
            if(spacing<0f) throw new ArgumentOutOfRangeException(nameof(spacing));

            float center=(playerCount+1)*0.5f;
            return (playerID-center)*spacing;
        }
    }
}
