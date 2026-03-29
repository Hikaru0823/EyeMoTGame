using System.IO;
using System.Xml.Serialization;
using UnityEngine;

public class XmlLoader
{
    public static T LoadXml<T>(string path) where T : class
    {
        if (!File.Exists(path))
        {
            Debug.LogError($"XMLファイルが見つかりません: {path}");
            return null;
        }

        try
        {
            XmlSerializer serializer = new XmlSerializer(typeof(T));

            using (FileStream stream = new FileStream(path, FileMode.Open))
            {
                return serializer.Deserialize(stream) as T;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"XML読み込み失敗: {e}");
            return null;
        }
    }
}
