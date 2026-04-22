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

    public static Action<float> OnTimeUpdated;
	
	// ゲーム開始からの経過時間を計算するプロパティ
	public static float Time => Instance?.Object?.IsValid == true
		? (Instance.TickStarted == 0 
			? 0
			: (Instance.Runner.Tick - Instance.TickStarted) * Instance.Runner.DeltaTime)
		: 0;

    public override void Spawned()
    {
        Instance = this;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        Instance = null;
    }

    public override void FixedUpdateNetwork()
    {
        if(TickStarted == 0) return;
        OnTimeUpdated?.Invoke(Time);
    }
}
