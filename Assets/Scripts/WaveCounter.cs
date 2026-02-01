using UnityEngine;
using TMPro;

public class WaveCounter : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI WaveText;
    private WaveGenerator waveGenerator;
    // Update is called once per frame

    void Awake()
    {
        waveGenerator = FindFirstObjectByType<WaveGenerator>();
    }
    
    void Update()
    {
        if (WaveText?.text != null)
        {
            if (waveGenerator.waveText == null)
            {
                WaveText.text = "";
            }
            else
            {
                WaveText.text = waveGenerator.waveText;
            }
        }
        
    }
}
