using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerData : Singleton<PlayerData>
{
    public string Nickname { get; set; } = "";
    public string CharacterName { get; set; } = "Character";
    public Sprite PlayerImage { get; set; }

    public bool CanUseShortCut = true;
}
