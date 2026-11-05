using System.Globalization;
using UnityEngine;

/// <summary>
/// Stable receiver for messages sent by the web page to the Unity WebGL player.
/// </summary>
public sealed class SignPlaybackBridge : MonoBehaviour
{
    public const string ReceiverName = "ISLSignBridge";

    private static SignPlaybackBridge instance;
    private SignPlaybackController playbackController;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateReceiver()
    {
        if (instance != null || GameObject.Find(ReceiverName) != null)
        {
            return;
        }

        GameObject receiver = new GameObject(ReceiverName);
        DontDestroyOnLoad(receiver);
        instance = receiver.AddComponent<SignPlaybackBridge>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        gameObject.name = ReceiverName;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        FindPlaybackController();
    }

    public void PlaySign(string signId)
    {
        if (FindPlaybackController())
        {
            playbackController.PlaySign(signId);
        }
        else
        {
            Debug.LogError("SignPlaybackBridge: No SignPlaybackController is present in the scene.");
        }
    }

    public void SetPlaybackSpeed(string speed)
    {
        if (!float.TryParse(speed, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedSpeed))
        {
            return;
        }

        if (FindPlaybackController())
        {
            playbackController.SetPlaybackSpeed(parsedSpeed);
        }
    }

    private bool FindPlaybackController()
    {
        if (playbackController == null)
        {
            playbackController = FindObjectOfType<SignPlaybackController>();
        }

        return playbackController != null;
    }
}
