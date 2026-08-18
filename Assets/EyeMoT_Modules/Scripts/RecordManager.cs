using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using EyeMoT.Balloon;
using EyeMoT.Heatmap;
using UnityEngine;

namespace EyeMoT
{
    public class RecordManager : Singleton<RecordManager>
    {
        [SerializeField] private string _saveDir = "/YOUR_RECORD/";
        [SerializeField] private HeatmapRenderer _heatmapRenderer;
        [SerializeField] private GameRecoder _gameRecoder;
        [SerializeField] private FocusCalmManager _focusCalmManager;
        
        private string _recordPath => System.IO.Path.GetDirectoryName(Application.dataPath) + _saveDir + $"/{Application.productName + "_" + _startDate.ToString("yyyyMMddHHmmss")}/";
        private bool _isRecording = false;
        private ConcurrentQueue<RecordSample> _receivedData = new();
        private List<string[]> _recordedData = new();
        private List<string> _types = new();
        private DateTime _startDate;
        private string _fileName;

        void ClearData()
        {
            _receivedData.Clear();
            _recordedData.Clear();
            _types.Clear();
        }

        public void Enqueue(RecordSample data)
        {
            if (data == null) return;
            if(!_isRecording) return;
            _receivedData.Enqueue(data);
        }

        public void StartRecord(bool gameRecord = true)
        {
            ClearData();

            _isRecording = true;
            _startDate = System.DateTime.Now;
            _fileName = Application.productName + "_" + _startDate.ToString("yyyyMMddHHmmss");
            _types.Add("#GameTime");
            _types.AddRange(BalloonSpawnManager.Instance._csvType);
            if(_heatmapRenderer != null)
            {
                _heatmapRenderer.StartHeatmap(true, onDataReceived: Enqueue);
                _types.AddRange(_heatmapRenderer.Type);
            }
            if(_focusCalmManager != null)
            {
                _focusCalmManager?.StartRecord(Enqueue);
                _types.AddRange(_focusCalmManager.Type);
            }
            if(gameRecord)
                _gameRecoder?.RecordStart(_recordPath, _fileName);
        }

        public void StopRecord(bool writeCSV = true)
        {
            _isRecording = false;
            _heatmapRenderer?.StopHeatmap();
            _gameRecoder?.RecordEnd();
            _focusCalmManager?.StopRecord();

            if(writeCSV)
            {
                WriteCsv(_recordedData);
            }
        }

        public void WriteCsv(List<string[]> data)
        {
            #if UNITY_WEBGL && !UNITY_EDITOR
            return;
            #endif
            data = MergeRowsByTimestamp(data);
            if (!System.IO.Directory.Exists(_recordPath))
            {
                System.IO.Directory.CreateDirectory(_recordPath);
            }

            var csvManager = new CSVManager();
            var writeData = new List<string[]>();
            writeData.Add(new string[] { "#Date" });
            writeData.Add(new string[] { _startDate.ToString("yyyy/MM/dd HH:mm:ss") });
            writeData.Add(new string[] { "#Screen_X", "Screen_Y", "DataCount" });
            writeData.Add(new string[] { Screen.width.ToString(), Screen.height.ToString(), data.Count.ToString() });
            writeData.Add(_types.ToArray());
            writeData.AddRange(data);
            csvManager.CSVWrite(writeData, _recordPath + _fileName + ".csv", isAppend: false);

        }

        private List<string[]> MergeRowsByTimestamp(List<string[]> data)
        {
            var mergedData = new List<string[]>();
            if(data == null) return mergedData;

            var rowByTimestamp = new Dictionary<string, string[]>();
            foreach(var row in data)
            {
                if(row == null || row.Length == 0) continue;

                string timestamp = row[0];
                if(string.IsNullOrEmpty(timestamp))
                {
                    mergedData.Add(row);
                    continue;
                }

                if(!rowByTimestamp.TryGetValue(timestamp, out var mergedRow))
                {
                    mergedRow = new string[_types.Count];
                    rowByTimestamp.Add(timestamp, mergedRow);
                    mergedData.Add(mergedRow);
                }

                int length = Mathf.Min(row.Length, mergedRow.Length);
                for(int i = 0; i < length; i++)
                {
                    if(!string.IsNullOrEmpty(row[i]))
                    {
                        mergedRow[i] = row[i];
                    }
                }
            }

            FillEmptyValuesWithLatest(mergedData);
            return mergedData;
        }

        private void FillEmptyValuesWithLatest(List<string[]> data)
        {
            if(data == null || data.Count == 0) return;

            var latestValues = new string[_types.Count];
            for(int i = 1; i < latestValues.Length; i++)
            {
                latestValues[i] = "0";
            }

            foreach(var row in data)
            {
                if(row == null) continue;

                int length = Mathf.Min(row.Length, latestValues.Length);
                for(int i = 1; i < length; i++)
                {
                    if(string.IsNullOrEmpty(row[i]))
                    {
                        row[i] = latestValues[i];
                    }
                    else
                    {
                        latestValues[i] = row[i];
                    }
                }
            }
        }

        void Update()
        {
            if(!_isRecording) return;

            while(_receivedData.TryDequeue(out var data))
            {
                string[] recordData = new string[_types.Count];
                recordData[0] = data.TimeStamp.ToString("F2");
                for(int i = 0; i < data.Type.Length; i++)
                {
                    int index = _types.IndexOf(data.Type[i]);
                    if(index >= 0)
                    {
                        recordData[index] = data.Data[i];
                    }
                }
                _recordedData.Add(recordData);
            }
        }

        void OnApplicationQuit()
        {
            if(_isRecording)
            {
                StopRecord(false);
            }
        }
    }

    public class RecordSample
    {
        public float TimeStamp;
        public string[] Type;
        public string[] Data;
    }
}
