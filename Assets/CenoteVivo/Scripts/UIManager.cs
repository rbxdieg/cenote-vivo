using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CenoteVivo
{
    // Coordina la interfaz: menú de inicio con frases de impacto, banner superior,
    // panel desplegable de concientización y los controles de día/noche, contaminación y vista.
    public class UIManager : MonoBehaviour
    {
        [Header("Sistemas")]
        [SerializeField] DayNightController dayNight;
        [SerializeField] PollutionSimulator pollution;
        [SerializeField] PerspectiveCameraController cameras;

        [Header("Menú de inicio")]
        [SerializeField] GameObject menuPanel;
        [SerializeField] Text titleText;
        [SerializeField] Text subtitleText;
        [SerializeField] Text phraseText;
        [SerializeField] CanvasGroup phraseGroup;
        [SerializeField] Button startButton;
        [SerializeField] float phraseSeconds = 5f;

        [Header("Pantalla principal")]
        [SerializeField] GameObject hudPanel;
        [SerializeField] Text bannerText;
        [SerializeField] Button infoButton;
        [SerializeField] Button dayNightButton;
        [SerializeField] Text dayNightLabel;
        [SerializeField] Button viewButton;
        [SerializeField] Text viewLabel;
        [SerializeField] Slider pollutionSlider;
        [SerializeField] Text pollutionLabel;
        [SerializeField] Text pollutionCaption;
        [SerializeField] Button pollutionToggle;
        [SerializeField] Text pollutionToggleLabel;
        [SerializeField] Image pollutionFill;
        [SerializeField] Color cleanColor = new Color(0.18f, 0.77f, 0.71f);
        [SerializeField] Color pollutedColor = new Color(0.55f, 0.43f, 0.12f);

        [Header("Panel informativo")]
        [SerializeField] GameObject infoPanel;
        [SerializeField] Text infoTitle;
        [SerializeField] Text infoBody;
        [SerializeField] Button closeInfoButton;
        [SerializeField] ScrollRect infoScroll;

        Coroutine phrases;

        void Awake()
        {
            titleText.text = AwarenessContent.Title;
            subtitleText.text = AwarenessContent.Subtitle;
            bannerText.text = AwarenessContent.Banner;
            infoTitle.text = AwarenessContent.InfoTitle;
            infoBody.text = AwarenessContent.InfoBody;

            startButton.onClick.AddListener(StartExperience);
            infoButton.onClick.AddListener(() => ShowInfo(true));
            closeInfoButton.onClick.AddListener(() => ShowInfo(false));
            dayNightButton.onClick.AddListener(dayNight.Toggle);
            viewButton.onClick.AddListener(cameras.Next);
            pollutionToggle.onClick.AddListener(() =>
            {
                pollution.Toggle();
                pollutionSlider.SetValueWithoutNotify(pollution.Target);
                RefreshPollution(pollution.Level);
            });
            pollutionSlider.onValueChanged.AddListener(pollution.SetTarget);

            dayNight.OnModeChanged += night => dayNightLabel.text = night ? "Noche" : "Día";
            cameras.OnViewChanged += (_, label) => viewLabel.text = label;
            pollution.OnLevelChanged += RefreshPollution;
        }

        void Start()
        {
            dayNightLabel.text = dayNight.IsNight ? "Noche" : "Día";
            viewLabel.text = cameras.CurrentLabel;
            pollutionSlider.SetValueWithoutNotify(pollution.Target);
            RefreshPollution(pollution.Level);
            ShowMenu();
        }

        public void ShowMenu()
        {
            menuPanel.SetActive(true);
            hudPanel.SetActive(false);
            infoPanel.SetActive(false);
            cameras.GoTo(0);
            cameras.Orbiting = true;
            if (phrases != null) StopCoroutine(phrases);
            phrases = StartCoroutine(CyclePhrases());
        }

        public void StartExperience()
        {
            if (phrases != null) StopCoroutine(phrases);
            menuPanel.SetActive(false);
            hudPanel.SetActive(true);
            cameras.Orbiting = false;
            cameras.GoTo(0);
        }

        public void ShowInfo(bool show)
        {
            infoPanel.SetActive(show);
            if (show && infoScroll != null) infoScroll.verticalNormalizedPosition = 1f;
        }

        void RefreshPollution(float level)
        {
            int percent = Mathf.RoundToInt(level * 100f);
            string state = level < 0.5f ? AwarenessContent.CleanState : AwarenessContent.PollutedState;
            pollutionLabel.text = $"Estado del cenote: {state} ({percent}%)";
            pollutionCaption.text = AwarenessContent.PollutionCaption(level);
            pollutionToggleLabel.text = pollution.IsPolluted ? "Restaurar" : "Contaminar";
            pollutionSlider.SetValueWithoutNotify(pollution.Target);
            if (pollutionFill != null) pollutionFill.color = Color.Lerp(cleanColor, pollutedColor, level);
        }

        IEnumerator CyclePhrases()
        {
            var list = AwarenessContent.ImpactPhrases;
            for (int i = 0; ; i = (i + 1) % list.Length)
            {
                phraseText.text = list[i];
                yield return Fade(0f, 1f, 0.6f);
                yield return new WaitForSeconds(phraseSeconds);
                yield return Fade(1f, 0f, 0.6f);
            }
        }

        IEnumerator Fade(float from, float to, float seconds)
        {
            for (float t = 0f; t < 1f; t += Time.deltaTime / seconds)
            {
                phraseGroup.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }
            phraseGroup.alpha = to;
        }

        void Update()
        {
            // Botón "atrás" de Android: cierra el panel o vuelve al menú.
            if (UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            {
                if (infoPanel.activeSelf) ShowInfo(false);
                else if (hudPanel.activeSelf) ShowMenu();
            }
        }
    }
}
