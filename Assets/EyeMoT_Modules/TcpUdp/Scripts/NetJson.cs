using Newtonsoft.Json;

public static class NetJson
{
    public static string ToJson<TPayload>(TPayload obj, string type, int targetId = -1, int senderId = -1)
    {
        var msg = new NetMessage<TPayload>
        {
            Type = type,
            SenderId = senderId,
            TargetId = targetId,
            Payload = obj
        };
        return ToJson(msg);
    }

    public static string ToJson(object obj)
    {
        return JsonConvert.SerializeObject(obj);
    }

    public static T FromJson<T>(string json)
    {
        return JsonConvert.DeserializeObject<T>(json);
    }
}

public class StringPayload
{
    public string Text;
}

public class Vector3Payload
{
    public float X;
    public float Y;
    public float Z;
}

public class EffectCreatePayload
{
    public int VFXTypeIndex;
    public bool CanPlaySE;
    public bool CanPlayLowSE;
    public float X;
    public float Y;
    public float Z;
}

public class ImageCreatePayload
{
    public string ImageKey;
    public int AnimationTypeIndex;
    public string ImageGUID;
    public float X;
    public float Y;
}

public class ImagePositionPayload
{
    public string ImageGUID;
    public float X;
    public float Y;
}

public class NetMessage<TPayload>
{
    public string Type;
    public int SenderId;     // 送信元 clientId 　-2: Broadcast, -1: Server
    public int TargetId;     // 送信先 clientId 　-2: Broadcast, -1: Server
    public TPayload Payload;
}

public class NetMessageType
{
    public const string UdpConnectRequest = "UdpConnectRequest";
    public const string RegisteredClient = "RegisteredClient";
    public const string ClientObjectCreate = "ClientObjectCreate";
    public const string MousePosition = "MousePosition";
    public const string EffectCreate = "EffectCreate";
    public const string ImageCreate = "ImageCreate";
    public const string ImagePosition = "ImagePosition";
    public const string ImageActive = "ImageActive";
    public const string ImageDestroy = "ImageDestroy";
    public const string EyeMoTMouseStatus = "EyeMoTMouseStatus";
    public const string RecordStart = "RecordStart";
}
