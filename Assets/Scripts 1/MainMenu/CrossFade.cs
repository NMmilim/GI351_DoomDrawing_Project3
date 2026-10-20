using System.Collections;
using UnityEngine;

[System.Serializable]
public class CrossFade : SceneTransition
{
    public CanvasGroup crossFade;
    public float transitionDuration = 1f; // Customizable fade time

    public override IEnumerator AnimateTransitionIn()
    {
        // Native fade from current alpha up to 1.0
        yield return StartCoroutine(FadeCanvasGroup(1f));
    }

    public override IEnumerator AnimateTransitionOut()
    {
        // Native fade from current alpha down to 0.0
        yield return StartCoroutine(FadeCanvasGroup(0f));
    }

    // Completely native animation helper loop
    private IEnumerator FadeCanvasGroup(float targetAlpha)
    {
        if (crossFade == null) yield break;

        while (!Mathf.Approximately(crossFade.alpha, targetAlpha))
        {
            // Move alpha smoothly toward target frame by frame
            crossFade.alpha = Mathf.MoveTowards(
                crossFade.alpha,
                targetAlpha,
                Time.deltaTime / transitionDuration
            );

            yield return null; // Wait for the next frame
        }

        // Snap precisely to the target value at the end
        crossFade.alpha = targetAlpha;
    }
}