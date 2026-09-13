using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class CloseObject : MonoBehaviour
{

    public GameObject player;
    private float distance;
    private SpriteRenderer sprite;
    
    // Start is called before the first frame update
    void Start()
    {
        sprite = gameObject.GetComponent<SpriteRenderer>();
    }

    // Update is called once per frame
    void Update()
    {

        distance = Vector3.Distance(player.transform.position, gameObject.transform.position);
        
        if (distance < 5f)
        {
            Debug.Log("this is distance" +  distance);
            sprite.color = Color.green;
        }
        else
        {
            sprite.color = Color.yellow;
        }
    }

}
