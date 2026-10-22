using System;
using UnityEngine;

[Serializable]
public class FaxMessage
{
    public string id;
    public string title;
    public string textureName;
    public float time;
    public string trigger;

    [NonSerialized] public Texture2D texture;
}
