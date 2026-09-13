using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;
using System.Security.Cryptography;

public class ClueSystem : MonoBehaviour
{
    public GameObject player;

    private bool exitDialogShown { get; set; } = false;
    
    //public GameObject camera;
    public List<GameObject> clues;
    // private SpriteRenderer spriteRenderer;
    private GameObject currentClue = null;
    private List<string> colours =
        new() { "blue", "green", "red", "yellow", "black", "purple", "orange" };

    private List<string> notz =
        new() { "high treason", "corruption", "mutiny" };

    private List<string> guns =
        new() { "Glock", "AWP", "M4A1-S", "Desert Eagle", "UPS", "Bazooka" };

    private List<string> persons =
        new() { "unknown woman", "mafia boss", "Jacket Rick", "Evil Morty" };

    private List<string> shows =
        new()
        {
            "Stranger Things", "Rings of Power", "Game of Thrones",
            "House of the Dragons"
        };

    private List<string> actions =
        new()
        {
            "Cheat on his wife",
            "kill the neighbour's dog",
            "hit your sister"
        };
    
    private List<string> membership =
        new()
        {
            "KKK",
            "New-Age Nazi",
            "A Korean boy band"
        };
    
    private List<string> music =
        new()
        {
            "Pop",
            "Country",
            "METAL"
        };
    
    private List<string> cats =
        new()
        {
            "poisonous fangs",
            "claws of steel",
            "fur which spreads radiation"
        };
    
    private List<string> clueTextures = new()
    {
        "Sprites/ggj-2023/Mock-up drop",
        "Sprites/ggj-2023/Mock-up pests",
        "Sprites/ggj-2023/Mock-up rock",
    };

    public List<ClueData> clueDatas      = new();
    public static List<ClueData> cluesPlaced    = new();
    public List<ClueData> clueDatasFound = new();

    // void Shuffle<T>(IList<T> list)
    // {
    //     RNGCryptoServiceProvider provider = new RNGCryptoServiceProvider();
    //     int n = list.Count;
    //     while (n > 1)
    //     {
    //         byte[] box = new byte[1];
    //         do provider.GetBytes(box);
    //         while (!(box[0] < n * (Byte.MaxValue / n)));
    //         int k = (box[0] % n);
    //         n--;
    //         T value = list[k];
    //         list[k] = list[n];
    //         list[n] = value;
    //     }
    // }
    
    void Start()
    {
        Statics.answeredQuestions = 0;
        clueDatas = new();
        cluesPlaced = new();
        clueDatasFound = new();

        clueDatas = createStory();
        // spriteRenderer = GetComponent<SpriteRenderer>();
        clues = new();
        // GameObject[] spawnPoints =
        //     GameObject.FindGameObjectsWithTag("clue_spawn_point");

        // List<GameObject> spawnPoints2 = new();

        //foreach (var sp in spawnPoints)
        //{
        //    spawnPoints2.Add(sp);
        //}

        // spawnPoints2 = spawnPoints.OrderBy(sp => Guid.NewGuid()).ToList();

        var list = new List<Vector2>();
        for (var x = 0; x < 26 * 2; x++) {
            for (var y = 4; y < 46; y++) {
                list.Add(new Vector2(x - 26, -y));
            }
        }
        // var shuffledList = list.OrderBy(a => Guid.NewGuid()).ToList();

        for (int i = 0; i < list.Count; i++) {
            var temp = list[i];
            int randomIndex = Random.Range(i, list.Count);
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }

        // Debug.Log(list.ElementAt(0));
        Debug.Log(list.First());
        Debug.Log(list.Last());

        var numWater = 32;
        var numPest = 64;
        var numRock = 338;

        for (var i = 0; i < numWater; i++)
        {
            var cd = clueDatas[0];
            
            //(int, int) pos = cluePositions.First();
            //cluePositions.RemoveAt(0);
            // GameObject go = spawnPoints2.First();
            // spawnPoints2.RemoveAt(0);
            
            SpawnClue(list[i].x, list[i].y, cd, "Sprites/ggj-2023/Mock-up drop");

            cluesPlaced.Add(cd);
        }

        for (var i = 0; i < numPest; i++)
        {
            var cd = clueDatas[0];

            SpawnClue(list[i + numWater].x, list[i + numWater].y, cd, "Sprites/ggj-2023/Mock-up pests");

            cluesPlaced.Add(cd);
        }

        for (var i = 0; i < numRock; i++)
        {
            var cd = clueDatas[0];
            
            SpawnClue(list[i + numWater + numPest].x, list[i + numWater + numPest].y, cd, "Sprites/ggj-2023/Mock-up rock");

            cluesPlaced.Add(cd);
        }

        //SpawnClue(300, 170);
    }

    void SpawnClue(float x, float y, ClueData clueData, string itemPath)
    {
        GameObject clueGo = new GameObject();
        clueGo.transform.position = Vector3.zero + new Vector3(x, y, 1);
        clueGo.transform.localScale = new Vector3(0.2777778f, 0.2777778f, 1f);
        
        Clue clue = clueGo.AddComponent<Clue>();
        clue.go = clueGo;
        clue.clueData = clueData;
        
        SpriteRenderer spriteRenderer = clueGo.AddComponent<SpriteRenderer>();
        spriteRenderer.enabled = true;
        //spriteRenderer.sprite = Resources.Load<Sprite>("Sprites/IMG_2564");

        // for (int i = 0; i < clueTextures.Count; i++) {
        //     var temp = clueTextures[i];
        //     int randomIndex = Random.Range(i, clueTextures.Count);
        //     clueTextures[i] = clueTextures[randomIndex];
        //     clueTextures[randomIndex] = temp;
        // }

        // var itemPath = clueTextures[0];
        
        clueGo.name = $"item-{itemPath}";
        if (itemPath.Contains("Sprites/ggj-2023/Mock-up rock")) {
            clueGo.name = $"{itemPath}";
        }
        spriteRenderer.sprite = Resources.Load<Sprite>(itemPath);

        // spriteRenderer.sprite = Resources.Load<Sprite>("Sprites/" + clueData.graphic.Item1);
        //spriteRenderer.sprite = Resources.Load<Sprite>(randomClueTexture());
        spriteRenderer.sortingOrder = 1;

        var boxCollider = clueGo.AddComponent<BoxCollider2D>();
        // boxCollider.size = new Vector2(4, 4);

        // var rigidBody = clueGo.AddComponent<Rigidbody2D>();
        // rigidBody.isKinematic = true;

        clues.Add(clueGo);
    }
    
    private void FixedUpdate()
    {
        // spriteRenderer.enabled = false;
        foreach (GameObject clue in clues)
        {
            if ((clue) && ((clue.transform.position - player.transform.position).magnitude < 5.0f))
            {
                // Do something here
                //Debug.Log("Player is close to: ???");
                //TakeClue = alse;
                // spriteRenderer.enabled = true;
                currentClue = clue;
                return;
            }
        }

    }

    // private void Update()
    // {
    //     if (exitDialogShown && Input.GetKeyDown(KeyCode.Space))
    //     {
    //         SceneManager.LoadScene("questions");
    //     }
        
    //     cluesFoundText.SetText($"Clues: {clueDatasFound.Count()}/{cluesPlaced.Count()}");

    //     if (!dialogueManager.animator.GetBool("IsOpen") && ((Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E)) && spriteRenderer.enabled))
    //     {
    //         Debug.Log("X");
    //         Clue clue = currentClue.GetComponent<Clue>();
    //         if (clue != null)
    //         {
    //             dialogueManager.TriggerDialogue(clue);
    //             clueDatasFound.Add(clue.clueData);
    //             Destroy(currentClue);
    //         }
    //     }
    //     else if (dialogueManager.animator.GetBool("IsOpen") && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape)))
    //     {
    //         Debug.Log("Removing dialog");
    //         dialogueManager.EndDialogue();
    //         if (clueDatasFound.Count() == cluesPlaced.Count())
    //         {
    //             exitDialogShown = true;
    //             skipToNextStep.gameObject.SetActive(true);
    //         }
    //     }
        
    //     if (Input.GetKeyDown(KeyCode.O))
    //     {
    //         Debug.Log("Questioning scene");
    //         //Debug.Log($"Size of cluesPlaced: {cluesPlaced.Count()}");
    //         //DataSaver.saveData(cluesPlaced, "cluesPlaced");
    //         SceneManager.LoadScene("questions");
    //     }
    // }

    private List<string> randomizedColours()
    {
        return colours.OrderBy(i => Guid.NewGuid()).ToList();
    }

    private List<string> randomizedNotz()
    {
        return notz.OrderBy(i => Guid.NewGuid()).ToList();
    }

    private List<string> randomizedList(List<string> list)
    {
        return list.OrderBy(item => Guid.NewGuid()).ToList();
    }
    
    private List<ClueData> createStory()
    {
        List<ClueData> clueData = new();

        ClueData cd0 = createClueData(
            colours,
            "President's car",
            "The president was seen in a {0} car",
            "What colour was the president's car?",
            3,
            true);
        clueData.Add(cd0);

        ClueData cd1 = createClueData(
            notz,
            "A corrupt president",
            "Mr. President, you are corrupt, you're convicted of {0}",
            "What was the president convicted of?",
            3,
            true);
        clueData.Add(cd1);

        ClueData cd2 = createClueData(
            guns,
            "A trigger happy president",
            "The president aimed a {0} at a person",
            "Which gun was the president wielding?",
            3,
            true);
        clueData.Add(cd2);
        
        ClueData cd3 = createClueData(
            persons,
            "The president's cabal",
            "{0} was seen with the president in a dark alley",
            "Who was the president seen with?",
            3,
            true);
        clueData.Add(cd3);

        ClueData cd4 = createClueData(
            shows,
            "Strange preferences",
            "{0} is the President's favourite show.",
            "Which show does the President like?",
            3,
            true);
        clueData.Add(cd4);
        
        ClueData cd5 = createClueData(
            actions,
             "He Dun Wat",
             "The president did {0}.",
             "What did the president do?",
             3,
             true);
        clueData.Add(cd5);
        
        ClueData cd6 = createClueData(
            membership,
             "Member of",
             "The president is a member of {0}.",
             "President is rumoured to be a member of?",
             3,
             true);
        clueData.Add(cd6);
        
        ClueData cd7 = createClueData(
            music,
             "Music",
             "The president loves {0}.",
             "President is fascinated with music, which type does he like?",
             3,
             true);
        clueData.Add(cd7);
        
        ClueData cd8 = createClueData(
            cats,
             "Secret Cat",
             "The president's secret cat power is {0}.",
             "What is the president's secret?",
             3,
             true);
        clueData.Add(cd8);
        
        return clueData.OrderBy(cd => Guid.NewGuid()).ToList();
    }

    private ClueData createClueData(List<string> list, string clueName,
        string clueText, string clueQuestion, int answers, bool truthiness)
    {
        ClueData cd = new();
        cd.adjectives = randomizedList(list);
        cd.adjective = cd.adjectives.First();
        cd.adjectives.RemoveAt(0);
        cd.clueName = clueName;
        cd.clueText = clueText;
        cd.question = clueQuestion;
        cd.answers = answers;
        cd.graphic = Statics.randomClueGraphics();
        cd.truthiness = truthiness;
        return cd;
    }
    
    public string clueName()
    {
        int tmp = Random.Range(100000, 999999);
        return "clue" + tmp;
    }

    private string randomClueTexture()
    {
        return clueTextures.OrderBy(t => Guid.NewGuid()).ToList().First();
    }
}
