using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;

    [SerializeField] private SoundLibrary sfxLibrary;
    [SerializeField] private AudioSource sfx2DSource;

    private void Awake()
    {
        // Prevent duplicate SoundManagers.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Find or create the AudioSource.
        if (sfx2DSource == null)
        {
            sfx2DSource = GetComponent<AudioSource>();

            if (sfx2DSource == null)
            {
                sfx2DSource = gameObject.AddComponent<AudioSource>();
            }

            sfx2DSource.enabled = true;
        }

        // Make sure 2D sound is not spatialized.
        sfx2DSource.spatialBlend = 0f;

        if (sfxLibrary == null)
        {
            Debug.LogError(
                "SoundManager: SoundLibrary is not assigned!",
                this);
        }
    }

    public void PlaySound3D(AudioClip clip, Vector3 pos)
    {
        if (Instance != null && Instance != this)
        {
            Instance.PlaySound3D(clip, pos);
            return;
        }

        if (clip == null)
            return;

        AudioSource.PlayClipAtPoint(clip, pos);
    }

    public void PlaySound3D(string soundName, Vector3 pos)
    {
        if (Instance != null && Instance != this)
        {
            Instance.PlaySound3D(soundName, pos);
            return;
        }

        if (sfxLibrary == null)
            return;

        PlaySound3D(sfxLibrary.GetClipFromName(soundName), pos);
    }

    public void PlaySound2D(string soundName)
    {
        if (Instance != null && Instance != this)
        {
            Instance.PlaySound2D(soundName);
            return;
        }

        if (sfxLibrary == null)
        {
            Debug.LogWarning(
                "SoundManager: SoundLibrary is missing.",
                this);
            return;
        }

        if (sfx2DSource == null)
        {
            Debug.LogWarning(
                "SoundManager: AudioSource is missing.",
                this);
            return;
        }

        AudioClip clip = sfxLibrary.GetClipFromName(soundName);

        if (clip == null)
        {
            Debug.LogWarning(
                $"SoundManager: No audio clip found for '{soundName}'.",
                this);
            return;
        }

        // Diagnose a disabled AudioSource instead of attempting playback.
        if (!sfx2DSource.enabled)
        {
            Debug.LogWarning(
                "SoundManager: AudioSource is disabled. " +
                "Check whether another script disables it.",
                sfx2DSource);
            return;
        }

        if (!sfx2DSource.gameObject.activeInHierarchy)
        {
            Debug.LogWarning(
                "SoundManager: AudioSource GameObject or one of its parents " +
                "is inactive. Check the GameObject hierarchy.",
                sfx2DSource);
            return;
        }

        sfx2DSource.PlayOneShot(clip);
    }

    private void DiagnoseAudioHierarchy()
    {
        Transform current = sfx2DSource.transform;

        Debug.Log("=== SoundManager Audio Hierarchy ===");

        while (current != null)
        {
            Debug.Log(
                $"GameObject: {current.name} | " +
                $"Active Self: {current.gameObject.activeSelf} | " +
                $"Active In Hierarchy: {current.gameObject.activeInHierarchy}",
                current.gameObject
            );

            current = current.parent;
        }

        Debug.Log("=== End Audio Hierarchy ===");
    }
}
