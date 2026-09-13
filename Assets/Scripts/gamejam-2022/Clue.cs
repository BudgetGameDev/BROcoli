using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Clue : MonoBehaviour
{
    public GameObject go { get; set; }
    
    public ClueData clueData { get; set; }

    public Clue(ClueData clueData)
    {
        this.clueData = clueData;
        //go.transform.position = Vector3.zero;
        Debug.Log("I WAS CREATED!!");
    }

    public void Init(GameObject go)
    {
        this.go = go;
    }
}
