using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion;
using System;

public class Timer :  NetworkBehaviour
{
    public static Timer Instance { get; private set; }

    [Networked]
	public int TickStarted { get; set; }
    [Networked]
    public float LimitTime { get; set; }

    public static Action<float> OnTimeUpdated;
    public static Action onTimeUp;
	
	// ゲーム開始からの経過時間を計算するプロパティ
	public static float Time => Instance?.Object?.IsValid == true
		? (Instance.TickStarted == 0 
			? 0
			: (Instance.Runner.Tick - Instance.TickStarted) * Instance.Runner.DeltaTime)
		: 0;

    private bool _isStarted = false;

    public override void Spawned()
    {
        Instance = this;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        Instance = null;
    }

    public void StartTimer(int startTick, float limitTime = 0)
    {
        TickStarted = startTick;
        LimitTime = limitTime;
        _isStarted = true;
    }


    public override void FixedUpdateNetwork()
    {
        if(TickStarted == 0) return;
        OnTimeUpdated?.Invoke(LimitTime - Time);
        if(LimitTime > 0 && Time >= LimitTime && _isStarted)
        {
            _isStarted = false;
            TickStarted = 0;
            // タイムアップの処理をここに書く
            Rpc_ResetTimer();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void Rpc_ResetTimer()
    {
        onTimeUp?.Invoke();
    }
}
