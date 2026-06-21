using TMPro;
using UnityEngine;

public class AnimalPlacementUIController : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private string fallbackText = "Place the animal";

    private void Start()
    {
        if (titleText == null)
        {
            return;
        }

        if (AnimalRecognitionState.HasAnimal)
        {
            titleText.text = $"Recognized: {AnimalRecognitionState.DisplayName}";
        }
        else
        {
            titleText.text = fallbackText;
        }
    }
}
