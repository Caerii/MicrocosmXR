using Fusion.XR.Shared;
using Fusion.XR.Shared.Rig;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class AutoDestruction : MonoBehaviour
{
    public float timerDuration = 10f;
    float timer;
    public TextMeshProUGUI textMeshPro;

    // Start is called before the first frame update
    void Start()
    {
        timer = timerDuration;
        InvokeRepeating("UpdateTextEverySecond", 1f, 1f);
    }

    // Update is called once per frame
    void Update()
    {
        timer -= Time.deltaTime;

        if (timer <= 0)
        {
            Destroy(gameObject); // Destroy the object
        }

    }

    private void UpdateTextEverySecond()
    {
        if (textMeshPro != null)
        {
            textMeshPro.text = "Time left: " + Mathf.Round(timer) + "s";
        }

        
    }

}
