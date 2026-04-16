using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerData : Singleton<PlayerData>
{
    public string Nickname { get; set; } = "Player";
    public string CharacterName { get; set; } = "Character";
    public bool UseImage { get; set; }
    public byte[] ImageBytes { get; set; }
}
