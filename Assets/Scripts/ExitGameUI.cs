using UnityEngine;
using UnityEngine.UI;

public class ExitGameUI : MonoBehaviour
{
    [SerializeField] private Button exitButton;

    private void Start()
    {
        if (!exitButton)
            exitButton = CreateExitButton();

        if (exitButton)
            exitButton.onClick.AddListener(QuitGame);
    }

    private Button CreateExitButton()
    {
        Canvas canvas = GetComponent<Canvas>() ?? GetComponentInParent<Canvas>();
        if (!canvas) return null;

        GameObject buttonObject = new GameObject("ExitButton");
        buttonObject.transform.SetParent(canvas.transform, false);

        RectTransform rect = buttonObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-20f, -20f);
        rect.sizeDelta = new Vector2(160f, 40f);

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.8f, 0.2f, 0.2f, 0.9f);

        Button button = buttonObject.AddComponent<Button>();

        GameObject textObject = new GameObject("Text");
        textObject.transform.SetParent(buttonObject.transform, false);
        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text label = textObject.AddComponent<Text>();
        label.text = "Выход";
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        return button;
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
