using ECSFrameWork;
using PoolSystem;
using System;
using System.Collections.Generic;
using UnityEngine;
using View;

namespace FrameWork.NetworkSync
{
    /// <summary>
    /// 双客户端公网回归期间的 View Rollback 运行时审计。
    /// 只读取 World/View/Binder/Pool 状态，不参与逻辑模拟。
    /// </summary>
    public sealed class NetworkViewRollbackRuntimeAudit
    {
        private readonly World _world;
        private readonly ViewManager _viewManager;
        private readonly EntityViewBinder _binder;
        private readonly IReadOnlyList<NetworkPlayerBinding> _players;
        private readonly GameObject _playerPrefab;
        private readonly List<KeyValuePair<Entity,int>> _bindings=new(16);
        private readonly HashSet<int> _viewIDs=new();

        public int SampleCount { get; private set; }
        public int FailureCount { get; private set; }
        public int MaxViewCount { get; private set; }
        public int MaxBindingCount { get; private set; }
        public int MaxPoolInUseCount { get; private set; }
        public int LastViewCount { get; private set; }
        public int LastBindingCount { get; private set; }
        public int LastPoolInUseCount { get; private set; }
        public int LastSampledFrame { get; private set; }
        public string FirstFailure { get; private set; }

        public int CurrentViewCount => _viewManager?.ViewCount??0;
        public int CurrentBindingCount
        {
            get
            {
                if(_binder==null) return 0;
                return _binder.FillBindings(_bindings);
            }
        }
        public int CurrentPoolInUseCount => CountPoolInUse();

        public NetworkViewRollbackRuntimeAudit(
            World world,
            ViewManager viewManager,
            EntityViewBinder binder,
            IReadOnlyList<NetworkPlayerBinding> players,
            GameObject playerPrefab)
        {
            _world=world;
            _viewManager=viewManager;
            _binder=binder;
            _players=players;
            _playerPrefab=playerPrefab;
        }

        /// <summary>采样当前完整 View 状态；发现异常只记录第一次 Error，避免每帧刷屏。</summary>
        public void Sample(int frame)
        {
            if(_world==null||_viewManager==null||_binder==null||_players==null||_players.Count<=0)
                return;

            SampleCount++;

            int viewCount=_viewManager.ViewCount;
            int bindingCount=_binder.FillBindings(_bindings);
            int poolInUse=CountPoolInUse();

            LastSampledFrame=frame;
            LastViewCount=viewCount;
            LastBindingCount=bindingCount;
            LastPoolInUseCount=poolInUse;

            if(viewCount>MaxViewCount) MaxViewCount=viewCount;
            if(bindingCount>MaxBindingCount) MaxBindingCount=bindingCount;
            if(poolInUse>MaxPoolInUseCount) MaxPoolInUseCount=poolInUse;

            string failure=Validate(frame,viewCount,bindingCount,poolInUse);
            if(string.IsNullOrEmpty(failure)) return;

            FailureCount++;

            if(FirstFailure!=null) return;

            FirstFailure=failure;
            Debug.LogError($"NetworkViewRollbackRuntimeAudit Sample Error: {failure}");
        }

        private string Validate(int frame,int viewCount,int bindingCount,int poolInUse)
        {
            int expected=_players.Count;

            if(viewCount!=expected)
                return $"Frame={frame}, ViewCount Expected={expected} Actual={viewCount}";

            if(bindingCount!=expected)
                return $"Frame={frame}, BindingCount Expected={expected} Actual={bindingCount}";

            if(_playerPrefab!=null&&poolInUse!=expected)
                return $"Frame={frame}, PoolInUse Expected={expected} Actual={poolInUse}";

            _viewIDs.Clear();

            for(int i=0;i<_players.Count;i++)
            {
                NetworkPlayerBinding binding=_players[i];
                Entity entity=binding.Entity;

                if(!_world.IsAlive(entity))
                    return $"Frame={frame}, PlayerID={binding.PlayerID}, Entity Not Alive: {entity}";

                if(!_world.TryGetComponent(entity,out ViewPrefabComponent prefab)||prefab.prefabID<=0)
                    return $"Frame={frame}, PlayerID={binding.PlayerID}, ViewPrefabComponent Missing";

                if(!_world.TryGetComponent(entity,out ViewComponent view)||view.viewID<=0)
                    return $"Frame={frame}, PlayerID={binding.PlayerID}, ViewComponent Missing";

                if(!_viewIDs.Add(view.viewID))
                    return $"Frame={frame}, PlayerID={binding.PlayerID}, Duplicate ViewID={view.viewID}";

                if(!_viewManager.TryGetTransform(view.viewID,out Transform managerTransform)||managerTransform==null)
                    return $"Frame={frame}, PlayerID={binding.PlayerID}, ViewManager Missing ViewID={view.viewID}";

                if(!_binder.TryGetView(entity,out GameObject binderView)||binderView==null)
                    return $"Frame={frame}, PlayerID={binding.PlayerID}, Binder Missing View";

                if(!ReferenceEquals(managerTransform.gameObject,binderView))
                    return $"Frame={frame}, PlayerID={binding.PlayerID}, Binder/ViewManager Object Mismatch";

                if(!binderView.activeInHierarchy)
                    return $"Frame={frame}, PlayerID={binding.PlayerID}, Bound View Is Inactive";

                PoolItem item=binderView.GetComponent<PoolItem>();
                if(item==null)
                    return $"Frame={frame}, PlayerID={binding.PlayerID}, PoolItem Missing";

                if(item.IsInPool)
                    return $"Frame={frame}, PlayerID={binding.PlayerID}, Bound View Marked InPool";

                if(_playerPrefab!=null&&item.PrefabInstanceID!=_playerPrefab.GetInstanceID())
                    return $"Frame={frame}, PlayerID={binding.PlayerID}, PrefabInstanceID Mismatch";

                if(_world.TryGetComponent(entity,out PositionComponent position))
                {
                    Vector3 p=binderView.transform.position;
                    if(!NearlyEqual(position.x,p.x)||!NearlyEqual(position.y,p.y)||!NearlyEqual(position.z,p.z))
                        return $"Frame={frame}, PlayerID={binding.PlayerID}, Logic/View Position Mismatch, Logic=({position.x:F4},{position.y:F4},{position.z:F4}), View=({p.x:F4},{p.y:F4},{p.z:F4})";
                }
            }

            return null;
        }

        private int CountPoolInUse()
        {
            if(_playerPrefab==null) return 0;

            int prefabInstanceID=_playerPrefab.GetInstanceID();
            PoolItem[] items=UnityEngine.Object.FindObjectsOfType<PoolItem>(true);
            int count=0;

            for(int i=0;i<items.Length;i++)
            {
                PoolItem item=items[i];
                if(item!=null&&item.PrefabInstanceID==prefabInstanceID&&!item.IsInPool)
                    count++;
            }

            return count;
        }

        private static bool NearlyEqual(float a,float b)=>Mathf.Abs(a-b)<0.001f;
    }
}
