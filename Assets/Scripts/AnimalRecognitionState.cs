public static class AnimalRecognitionState
{
    public static string Label { get; private set; }
    public static string DisplayName { get; private set; }
    public static float Confidence { get; private set; }

    public static bool HasAnimal => !string.IsNullOrWhiteSpace(Label);

    public static void SetAnimal(string label, string displayName, float confidence)
    {
        Label = label;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? label : displayName;
        Confidence = confidence;
    }

    public static void Clear()
    {
        Label = null;
        DisplayName = null;
        Confidence = 0f;
    }
}
