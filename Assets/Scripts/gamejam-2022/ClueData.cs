using System;
using System.Collections.Generic;

[Serializable]
public struct ClueData
{
    //public string clueName { get; set; }
    //public string clueText { get; set; }
    //public string adjective { get; set; }
    //public string question { get; set; }
    //public List<string> adjectives { get; set; }
    //public bool truthiness { get; set; }
    //public int answers { get; set; }
    //public (string, string) graphic { get; set; }
    public string clueName;
    public string clueText;
    public string adjective;
    public string question;
    public List<string> adjectives;
    public bool truthiness;
    public int answers;
    public (string, string) graphic;
}
