using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Alternatives
{
    public double confidence;
    public string transcript;
}

[System.Serializable]
public class Results
{
    // keywords_result
    public Alternatives[] alternatives;
    public bool final;
}

[System.Serializable]
public class ResultData
{
    public Results[] results;
    public int result_index;
}