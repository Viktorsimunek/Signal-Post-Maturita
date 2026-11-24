using System;

[Serializable]
public class FaxDocument
{
    public string id;
    public string title = "INCOMING FAX";
    public string sender = "";
    public string body;
    public float quality = 0.75f;
    public bool CH_Logo;
    public int seed = 158;
}
