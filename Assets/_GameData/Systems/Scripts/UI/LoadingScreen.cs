using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Simple timed loading screen: a slider fills from 0 to 1 over _loadTime
/// seconds, then the next scene loads.
/// </summary>
public class LoadingScreen : MonoBehaviour
{
    [SerializeField] private float _loadTime = 3f;
    [SerializeField] private Slider _progressSlider;
    [SerializeField] private string _nextSceneName = "GameScene";
    private void Start()
    {
        StartCoroutine(LoadRoutine());
    }

    private IEnumerator LoadRoutine()
    {
        float duration = Mathf.Max(0.01f, _loadTime);
        float elapsed = 0f;

        if (_progressSlider != null)
            _progressSlider.value = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            if (_progressSlider != null)
                _progressSlider.value = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }

        if (_progressSlider != null)
            _progressSlider.value = 1f;

        SceneManager.LoadScene(_nextSceneName);
    }
}
