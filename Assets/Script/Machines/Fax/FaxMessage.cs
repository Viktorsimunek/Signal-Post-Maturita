using System;
using UnityEngine;

[Serializable]
public class FaxMessage
{
    public string id;
    public string title;
    public string sender;
    [TextArea(3, 12)] public string body;
    [Range(0, 1)] public float quality = 0.75f;
    public bool CH_Logo;
    public bool ScarySound;
    public int seed = 158;
    public string textureName;
    public float time;
    public string trigger;

    [NonSerialized] public Texture texture;
}
