using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


public class DialogueManager : MonoBehaviour
{
    public Clue clue;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI dialogueText;

    public Animator animator;
    public GameObject img;

    public AudioSource audio;
    public AudioClip audioEnterPaper;
    public AudioClip audioExitPaper;


    private Queue<string> clues; // first in first out


    private void Start()
    {
        clues = new Queue<string>();
    }

    public void StartDialogue (Dialogue dialogue)
    {
        Debug.Log("Starting dialogue");

        animator.SetBool("IsOpen", true);

        nameText.text = dialogue.clue;

        clues.Clear();

        foreach (string clue in dialogue.cluesForQueue)
        {
            clues.Enqueue(clue);
        }

        DisplayNextClue();
    }

    public void DisplayNextClue()
    {
        if (clues.Count == 0)
        {
            EndDialogue();
            return;
        }

        string clue = clues.Dequeue();
        dialogueText.text = clue;
        audio.clip = audioEnterPaper;
        audio.Play();
        Time.timeScale = 0;
        Debug.Log(clue);
    }

    public void EndDialogue()
    {
        Debug.Log("remove Dialogue");

        Time.timeScale = 1;
        
        audio.clip = audioExitPaper;
        audio.Play();

        animator.SetBool("IsOpen", false);
    }
    
    public void TriggerDialogue(Clue clue)
    {
        Dialogue d = new Dialogue();
        d.clue = clue.clueData.clueName;
        d.cluesForQueue = new[] { String.Format(clue.clueData.clueText, clue.clueData.adjective) };
        Image img2 = img.GetComponentInChildren<Image>();
        if (img2)
        {
            Debug.Log("img is not null");
            Debug.Log($"Icon: {clue.clueData.graphic.Item2}");
            img2.sprite = Resources.Load<Sprite>("Sprites/" + clue.clueData.graphic.Item2);
            //img2.sprite = Resources.Load<Sprite>("Sprites/IMG_2561");
        }
        else
        {
            Debug.Log("img is null");
        }
        //img.sprite = Resources.Load<Sprite>("/Resources/IMG_2561");
        //img.sprite = Resources.Load<Sprite>("Resources/IMG_2561");
        //Debug.Log($"Image: {img.sprite}");
        FindObjectOfType<DialogueManager>().StartDialogue(d);
    }
}
