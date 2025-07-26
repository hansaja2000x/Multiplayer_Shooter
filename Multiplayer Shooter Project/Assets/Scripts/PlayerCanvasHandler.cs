using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Networking;

public class PlayerCanvasHandler : MonoBehaviour
{
    [SerializeField] private GameObject otherPlayerCanvas;
    [SerializeField] private TextMeshProUGUI playerName;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private RawImage profileImage;

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

    public void SetProfileImage(string url)
    {
        if (!string.IsNullOrEmpty(url))
        {
            StartCoroutine(LoadProfileImage(url));
        }
    }

    private IEnumerator LoadProfileImage(string url)
    {
        using (UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(url))
        {
            yield return uwr.SendWebRequest();

            if (uwr.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Failed to load profile image: " + uwr.error);
            }
            else
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(uwr);
                profileImage.texture = texture;
            }
        }
    }

    public GameObject GetCanvasgameObject()
    {
        return otherPlayerCanvas;
    }
}