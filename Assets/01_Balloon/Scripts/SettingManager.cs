using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EyeMoT.Balloon
{
    public class SettingManager : SceneSingleton<SettingManager>
    {
        public GameData GameData {get; private set;} = new GameData();
        public BalloonData BalloonData {get; private set;} = new BalloonData();

        #region Setter
        public void SetBalloonAmount(string value) => GameData.BalloonAmount = int.Parse(value.Replace("個", ""));
        public void SetGameTime(string value) => GameData.GameTime = float.Parse(value.Replace("秒", ""));
        public void SetBalloonGeneratePatern(string value) => GameData.BalloonGeneratePatern = (BalloonSpawnManager.GenerationPatern)Enum.Parse(typeof(BalloonSpawnManager.GenerationPatern), value);
        public void SetBGColor(string value) => GameData.BGColor = (PreviewManager.BGColor)Enum.Parse(typeof(PreviewManager.BGColor), value);

        public void SetCollisionScale(string value) => BalloonData.CollisionScale = BalloonData.Table_CollisionScale[int.Parse(value.Replace("Level", "")) - 1];
        public void SetVisualScale(string value) => BalloonData.VisualScale = BalloonData.Table_VisualScale[int.Parse(value.Replace("Level", "")) - 1];
        public void SetLifeTime(string value) => BalloonData.LifeTime = float.Parse(value.Replace("秒", ""));
        public void SetVFXType(int idx) => BalloonData.VFXIdx = idx;
        #endregion
    }


    [Serializable]
    public class GameData
    {
        public int BalloonAmount;
        public float GameTime;
        public BalloonSpawnManager.GenerationPatern BalloonGeneratePatern;
        public PreviewManager.BGColor BGColor = PreviewManager.BGColor.Default;
    }

    [Serializable]
    public class BalloonData
    {
        public float CollisionScale;
        public float VisualScale;
        public float LifeTime;
        public int VFXIdx;

        public readonly float[] Table_CollisionScale = {1, 0.8f, 0.6f, 0.4f, 0.2f};
        public readonly float[] Table_VisualScale = {1, 1.2f, 1.4f, 1.6f, 1.8f};
    }
}