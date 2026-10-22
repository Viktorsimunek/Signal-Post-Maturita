using System;
using UnityEngine;

[Serializable]
public class RadioPerson
{
    public string id;
    public string speaker;
    public string voice;
    public string startNode;
    public RadioNode[] nodes;
}

[Serializable]
public class RadioNode
{
    public string id;
    [TextArea(2, 5)] public string text;
    public RadioAnswer[] answers;
}

[Serializable]
public class RadioAnswer
{
    public string text;
    [TextArea(2, 5)] public string reply;
    public string nextNode;
}

[Serializable]
public class RadioStationData
{
    public RadioStation[] stations;
}

[Serializable]
public class RadioStation
{
    public string name;
    [TextArea(1, 3)] public string text;
}
