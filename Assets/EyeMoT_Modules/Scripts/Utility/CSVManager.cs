using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class CSVManager
{
    public List<string[]> CSVRead(string dataDir, string dataFile, string delim=",")
    {
        var filePath = dataDir + "/" + dataFile;

        if (System.IO.Directory.Exists(dataDir))
        {
            if (!System.IO.File.Exists(filePath))
            {
                System.IO.File.Create(filePath);

                return new List<string[]>();
            }
        }
        else
        {
            System.IO.Directory.CreateDirectory(dataDir);

            System.IO.File.Create(filePath);

            return new List<string[]>();
        }

        List<string[]> data = new();

        using var dataText = new StreamReader(filePath);

        while (dataText.Peek() > -1)
        {
            string line = dataText.ReadLine().Replace("\\n", "\n");
            data.Add(line.Split(delim));
        }

        return data;
    }

    public void CSVWrite(List<string[]> writeDatas, string writeDataPath="write_data.csv", bool isAppend=false)
    {
        try
        {
            using var sw = new System.IO.StreamWriter(writeDataPath, append: isAppend);

            foreach (var data in writeDatas)
                sw.WriteLine(string.Join(",", data));
        }
        catch (System.Exception e)
        {
            Debug.Log(e.ToString());
        }
    }
    public void CSVNullWrite(string writeDataPath = "write_data.csv", bool isAppend = false)
    {
        try
        {
            using var sw = new System.IO.StreamWriter(writeDataPath, append: isAppend);

            sw.WriteLine("");
        }
        catch (System.Exception e)
        {
            Debug.Log(e.ToString());
        }
    }


    public List<string[]> CSVReadFromResource(string resourcePath, string delim=",")
    {
        List<string[]> csvs = new();

        TextAsset rawText = Resources.Load(resourcePath) as TextAsset;
        StringReader reader = new(rawText.text);

        while (reader.Peek() > -1)
        {
            string line = reader.ReadLine().Replace("\\n", "\n");
            csvs.Add(line.Split(delim));
        }

        return csvs;
    }
}
