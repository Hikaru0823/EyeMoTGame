using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace EyeMoT.Balloon
{
    public class SettingManager : SceneSingleton<SettingManager>
    {
        [SerializeField] private GameDataSelecter _gameDataSelecter;
        [SerializeField] private BalloonDataSelecter _balloonDataSelecter;
        public GameData GameData {get; private set;} = new GameData();
        public BalloonData BalloonData {get; private set;} = new BalloonData();

        override protected void OnAwake()
        {
            var saveData = Load();
            if(saveData != null)
            {
                GameData = saveData.GameData;
                BalloonData = saveData.BalloonData;
            }

            _gameDataSelecter.BalloonAmount.Initialize(Array.FindIndex(_gameDataSelecter.BalloonAmount.GetItems(), x => x.Contains(GameData.BalloonAmount.ToString() + " 個")) );
            _gameDataSelecter.GameTime.Initialize(Array.FindIndex(_gameDataSelecter.GameTime.GetItems(), x => x.Contains(GameData.GameTime.ToString() + " 秒")) );
            _gameDataSelecter.BalloonGeneratePatern.Initialize((int)GameData.BalloonGeneratePatern);
            _gameDataSelecter.BGColor.Initialize((int)GameData.BGColor);

            _balloonDataSelecter.CollisionScale.Initialize(Array.FindIndex(_balloonDataSelecter.CollisionScale.GetItems(), x => x.Contains(BalloonData.CollisionScale.ToString("F1") + " 倍")) );
            _balloonDataSelecter.VisualScale.Initialize(Array.FindIndex(_balloonDataSelecter.VisualScale.GetItems(), x => x.Contains(BalloonData.VisualScale.ToString("F1") + " 倍")) );
            _balloonDataSelecter.LifeTime.Initialize(Array.FindIndex(_balloonDataSelecter.LifeTime.GetItems(), x => x.Contains(BalloonData.LifeTime.ToString("F1") + " 秒")) );
            _balloonDataSelecter.VFXIdx.Initialize(BalloonData.VFXIdx);
        }

        void OnApplicationQuit()
        {
            Save();
        }

        public static void Save()
        {
            SaveData saveData = new SaveData
            {
                GameData = Instance.GameData,
                BalloonData = Instance.BalloonData
            };

            string json =JsonUtility.ToJson(saveData, true);
            ES3.Save<string>(SaveKeys.BalloonGameData, json);
            Debug.Log(json);
        }

        private static string FormatSaveJson(string json, SaveData saveData)
        {
            json = ReplaceJsonFloat(json, nameof(Instance.GameData.GameTime), saveData.GameData.GameTime, "0.#");
            json = ReplaceJsonFloat(json, nameof(Instance.BalloonData.CollisionScale), saveData.BalloonData.CollisionScale, "0.#");
            json = ReplaceJsonFloat(json, nameof(Instance.BalloonData.VisualScale), saveData.BalloonData.VisualScale, "0.#");
            json = ReplaceJsonFloat(json, nameof(Instance.BalloonData.LifeTime), saveData.BalloonData.LifeTime, "0.#");
            return json;
        }

        private static string ReplaceJsonFloat(string json, string key, float value, string format)
        {
            string formattedValue = value.ToString(format, CultureInfo.InvariantCulture);
            return Regex.Replace(
                json,
                $"(\"{Regex.Escape(key)}\"\\s*:\\s*)[-+]?\\d+(?:\\.\\d+)?(?:[eE][-+]?\\d+)?",
                match => match.Groups[1].Value + formattedValue);
        }

        public static SaveData Load()
        {
            if (!ES3.KeyExists(SaveKeys.BalloonGameData))
            {
                // Debug.LogWarning("セーブデータがありません");
                return null;
            }

            string json = ES3.Load<string>(SaveKeys.BalloonGameData);
            Debug.Log(json);
            SaveData saveData = JsonUtility.FromJson<SaveData>(json);

            return saveData;
        }

        #region Setter (UI Hook)
        public void SetBalloonAmount(string value) => GameData.BalloonAmount = int.Parse(value.Replace("個", ""));
        public void SetGameTime(string value) => GameData.GameTime = float.Parse(value.Replace("秒", ""));
        public void SetBalloonGeneratePatern(string value) => GameData.BalloonGeneratePatern = (BalloonSpawnManager.GenerationPatern)Enum.Parse(typeof(BalloonSpawnManager.GenerationPatern), value);
        public void SetBGColor(string value) => GameData.BGColor = (PreviewManager.BGColor)Enum.Parse(typeof(PreviewManager.BGColor), value);

        public void SetCollisionScale(string value) => BalloonData.CollisionScale = float.Parse(value.Replace("倍", ""));
        public void SetVisualScale(string value) => BalloonData.VisualScale = float.Parse(value.Replace("倍", ""));
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
    public class GameDataSelecter
    {
        public SelecterUI BalloonAmount;
        public SelecterUI GameTime;
        public SelecterUI BalloonGeneratePatern;
        public SelecterUI BGColor;
    }

    [Serializable]
    public class BalloonData
    {
        public float CollisionScale;

        public float VisualScale;
        public float LifeTime;
        public int VFXIdx;
    }

    [Serializable]
    public class BalloonDataSelecter
    {
        public SelecterUI CollisionScale;
        public SelecterUI VisualScale;
        public SelecterUI LifeTime;
        public SelecterUI VFXIdx;
    }

    [Serializable]
    public class SaveData
    {
        public GameData GameData;
        public BalloonData BalloonData;
    }
}
