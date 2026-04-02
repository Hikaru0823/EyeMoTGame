using System.Collections.Generic;
using UnityEngine;

namespace EyeMoT.Heatmap
{
    public class HeatmapCsvWriter
    {
        /// <summary>
        /// data format: List of [time, uv.x, uv.y]
        /// </summary>
        /// <param name="path"></param>
        /// <param name="uvList"></param>
        public static void WriteCsv(string path, float totalDistance, List<string[]> data)
        {
            #if UNITY_WEBGL && !UNITY_EDITOR
            return;
            #endif
            if (!System.IO.Directory.Exists(path))
            {
                System.IO.Directory.CreateDirectory(path);
            }
            var date = System.DateTime.Now;
            var csvManager = new CSVManager();
            var writeData = new List<string[]>();
            writeData.Add(new string[] { "#Date", "TotalDistance" });
            writeData.Add(new string[] { date.ToString("yyyy/MM/dd HH:mm:ss"), totalDistance.ToString("F1") });
            writeData.Add(new string[] { "#Screen_X", "Screen_Y", "GazeDataCount" });
            writeData.Add(new string[] { Screen.width.ToString(), Screen.height.ToString(), data.Count.ToString() });
            writeData.Add(new string[] { "#GameTime", "Gaze_X", "Gaze_Y" });
            writeData.AddRange(data);
            csvManager.CSVWrite(writeData, path + Application.productName + "_" + date.ToString("yyyyMMddHHmmss") + ".csv", isAppend: false);

        }
    }
}