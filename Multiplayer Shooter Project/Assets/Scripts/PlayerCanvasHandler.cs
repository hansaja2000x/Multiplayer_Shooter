using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class PlayerCanvasHandler : MonoBehaviour
{
    [SerializeField] private GameObject otherPlayerCanvas;
    [SerializeField] private TextMeshProUGUI playerName;
    [SerializeField] private Slider healthSlider;

    public void DeactivateCanvas()
    {
        otherPlayerCanvas.SetActive(false);
    }

    public void SetName(string name)
    {
        playerName.text = name;
    }

    public void SetHealth(float value)
    {
        healthSlider.value = value ;
    }

    public GameObject GetCanvasgameObject()
    {
        return otherPlayerCanvas;
    }
}
