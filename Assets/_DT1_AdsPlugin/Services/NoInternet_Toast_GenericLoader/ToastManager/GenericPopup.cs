using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GenericPopup : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI text;
    [SerializeField] private CanvasGroup canvasGroup;
    private Tween imageFadeTween, moveUpTween;

    public Action OnAnimationComplete { get; set; }

    public void Init(string text, Color color)
    {
        this.text.text = text;
        this.text.color = new Color(color.r, color.g, color.b, 1f);
        canvasGroup.alpha = 1f;
    }
    public void Animate()
    {
        gameObject.SetActive(true);
        DOVirtual.DelayedCall(0.6f, () =>
        {
            imageFadeTween = canvasGroup.DOFade(0f, 0.4f).SetUpdate(true);
        }).SetUpdate(true);
        moveUpTween = canvasGroup.transform.DOLocalMoveY(160f, 1f).SetEase(Ease.Linear).OnComplete(() =>
        {
             ReturnToPool();
        }).SetUpdate(true);
    }

    public void ReturnToPool()
    {
        OnAnimationComplete?.Invoke();
        canvasGroup.transform.localPosition = Vector3.zero;
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        imageFadeTween?.Kill();
        moveUpTween?.Kill();
    }
}
