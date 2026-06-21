using UnityEngine;

public class AnimalsListUI : MonoBehaviour
{
    [SerializeField] private GameObject listPanel;

    private void Awake()
    {
        if (listPanel != null)
        {
            listPanel.SetActive(false);
        }
    }

    public void Show()
    {
        if (listPanel != null)
        {
            listPanel.SetActive(true);
        }
    }

    public void Hide()
    {
        if (listPanel != null)
        {
            listPanel.SetActive(false);
        }
    }
}
