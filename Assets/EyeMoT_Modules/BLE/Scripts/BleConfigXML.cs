using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace EyeMoT.Ble
{
    [Serializable]
    [XmlRoot("BleConfig")]
    public class BleConfig
    {
        [XmlElement("SwitchBotData")]
        public SwitchBotData SwitchBot;

        [XmlElement("ArmOneDaData")]
        public ArmOneDaData ArmOneDa;
    }


    #region SwitchBot
    [Serializable]
    public class SwitchBotData
    {
        public string Version;
        public string ServiceUuid;
        public string CharacteristicUuid;
        public SwitchBotDef[] SwitchBots;

        public SwitchBotDef GetSwitchBotDefByType(byte type)
        {
            return SwitchBots.FirstOrDefault(x => x.TypeByte == type);
        }
    }

    [Serializable]
    public class SwitchBotDef
    {
        public string Name;
        public string Type;
        public string Payload;
        
        [XmlIgnore]
        public byte TypeByte
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Type))
                    return 0;

                string hex = Type.Trim();

                if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    hex = hex.Substring(2);

                return Convert.ToByte(hex, 16);
            }
        }

        [XmlIgnore]
        public byte[] PayloadBytes
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Payload))
                    return Array.Empty<byte>();

                return Payload
                    .Split(new[] { ' ', ',', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x =>
                    {
                        string hex = x.Trim();
                        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                            hex = hex.Substring(2);

                        return Convert.ToByte(hex, 16);
                    })
                    .ToArray();
            }
        }
    }
    #endregion

    #region ArmOneDa
    [Serializable]
    public class ArmOneDaData
    {
        public string Version;
        public string ServiceUuid;
        public string CharacteristicUuid;
        public string Indentifier;

        [XmlElement("Entry")]
        public List<ArmOneDaEntry> Entries;

        [XmlIgnore]
        public Dictionary<ArmOneDaCommand, string> CommandDic
        {
            get
        {
            var dict = new Dictionary<ArmOneDaCommand, string>();
            foreach (var e in Entries)
            {
                dict[e.Key] = e.Value;
            }
            return dict;
        }
        set
        {
            Entries = new List<ArmOneDaEntry>();
            if (value == null) return;

            foreach (var pair in value)
            {
                Entries.Add(new ArmOneDaEntry
                {
                    Key = pair.Key,
                    Value = pair.Value
                });
            }
        }
        }
    }

    public enum ArmOneDaCommand
    {
        Swing_On, Swing_Reverse_On, Swing_Off, Swing_Toggle, Rapid_On, Rapid_Reverse_On, Rapid_Off, Rapid_Toggle, AngleDecide_On, AngleDecide_Off
    }


    [Serializable]
    public class ArmOneDaEntry
    {
        [XmlAttribute("Key")]
        public ArmOneDaCommand Key;
        [XmlAttribute("Value")]
        public string Value;
    }
    #endregion
}