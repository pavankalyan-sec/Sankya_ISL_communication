using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;


// =========================================================
// SIGN PLAYBACK CONTROLLER
//
// Centralized playback controller that loads a MediaPipe
// Holistic Landmarks JSON file, manages a single frame
// counter with play / pause / stop / restart / set-frame,
// and dispatches the current FrameData to all listeners
// each frame.
//
// Replaces the per-script frame-counter logic that was
// previously duplicated in BodyController, HandController,
// etc.  Those scripts now subscribe to OnFrameUpdate
// instead of running their own timers.
//
// Uses shared types from LandmarkData.cs:
//   LandmarkFile, FrameData, VideoData, Landmark, etc.
// =========================================================


public class SignPlaybackController : MonoBehaviour
{
    // =====================================================
    // INSPECTOR FIELDS
    // =====================================================

    [Header("MediaPipe JSON")]
    [Tooltip("Drag ANY sign JSON here (e.g. apple.json, hello.json, or app.json) to test it in the Editor!")]
    public TextAsset clipJson;

    [Tooltip("Or type the sign name from StreamingAssets/signs/ (e.g. 'hello', 'apple', 'app') to auto-play on Start.")]
    public string initialSignName = "";


    [Header("Settings")]
    public bool playOnStart = false;

    [Range(0.1f, 3f)]
    public float playbackSpeed = 1f;

    public bool loop = false;

    [Tooltip("Interpolate pose and hand landmarks between captured frames to avoid stepped motion at the source video's frame rate.")]
    public bool interpolateLandmarks = true;


    // =====================================================
    // EVENTS
    // =====================================================

    // Event that fires each frame with the current FrameData.
    // Other scripts (BodyController, HandController, etc.)
    // subscribe to this in the Inspector or via code.

    [Serializable]
    public class FrameEvent : UnityEvent<FrameData> { }


    [Header("Events")]
    public FrameEvent OnFrameUpdate = new FrameEvent();

    // Fires after a sign's final frame is dispatched, or when playback is stopped.
    public UnityEvent OnPlaybackFinished = new UnityEvent();


    // =====================================================
    // PRIVATE STATE
    // =====================================================

    private LandmarkFile data;

    private int currentFrame;
    private float frameTimer;
    private float frameDuration = 1f / 30f;

    // Reused output frame; landmark arrays are resized only when the source
    // clip changes, so smooth playback does not allocate a new pose per tick.
    private FrameData interpolatedFrame;

    private bool playing;
    private int signLoadVersion;
    private string activeSignId = string.Empty;


    // =====================================================
    // UNITY LIFECYCLE
    // =====================================================


    private void Awake()
    {
        AutoWireControllers();
    }


    private void Start()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // The web prototype controls playback through SignPlaybackBridge.
        // Never start a scene-assigned sign automatically when the player loads.
        LoadJSON();
        return;
#else
        if (!string.IsNullOrEmpty(initialSignName))
        {
            PlaySign(initialSignName);
            return;
        }

        LoadJSON();

        if (playOnStart)
        {
            Play();
        }
#endif
    }


    private void AutoWireControllers()
    {
        // Automatically find and wire BodyController, HandController, FaceController
        // on this GameObject or in children/parents if not already wired.
        BodyController body = GetComponentInChildren<BodyController>();
        if (body == null) body = GetComponentInParent<BodyController>();
        if (body != null)
        {
            OnFrameUpdate.AddListener(body.ApplyFrame);
            OnPlaybackFinished.RemoveListener(body.ReturnToRestPose);
            OnPlaybackFinished.AddListener(body.ReturnToRestPose);
        }

        HandController[] hands = GetComponentsInChildren<HandController>();
        foreach (var hand in hands)
        {
            if (hand.enabled)
            {
                OnFrameUpdate.AddListener(hand.ApplyFrame);
                OnPlaybackFinished.RemoveListener(hand.ReturnToRestPose);
                OnPlaybackFinished.AddListener(hand.ReturnToRestPose);
            }
        }

        FaceController face = GetComponentInChildren<FaceController>();
        if (face == null) face = GetComponentInParent<FaceController>();
        if (face != null)
        {
            OnFrameUpdate.AddListener(face.ApplyFrame);
            OnPlaybackFinished.RemoveListener(face.ReturnToNeutral);
            OnPlaybackFinished.AddListener(face.ReturnToNeutral);
        }
    }


    private void Update()
    {
        if (!playing)
            return;

        if (data == null ||
            data.frames == null ||
            data.frames.Length == 0)
            return;


        // -------------------------------------------------
        // Advance the frame timer using the same
        // accumulator pattern as the old BodyController.
        // -------------------------------------------------

        frameTimer +=
            Time.deltaTime *
            playbackSpeed;

        bool finished = false;
        while (frameTimer >= frameDuration)
        {
            frameTimer -= frameDuration;

            currentFrame++;


            // ---------------------------------------------
            // Handle end-of-clip
            // ---------------------------------------------

            if (currentFrame >= data.frames.Length)
            {
                if (loop)
                {
                    // Wrap back to the first frame.
                    currentFrame = 0;
                }
                else
                {
                    // Clamp to the last valid frame,
                    // stop playback, and notify listeners.
                    currentFrame =
                        data.frames.Length - 1;

                    playing = false;
                    finished = true;

                    break;
                }
            }
        }


        // -------------------------------------------------
        // Dispatch the current frame to all listeners.
        // -------------------------------------------------

        DispatchCurrentFrame();

        // Dispatch the final sign frame first, then start the return-to-rest
        // transition so no listener reapplies the final pose over the reset.
        if (finished)
        {
            OnPlaybackFinished.Invoke();
        }
    }


    // =====================================================
    // JSON LOADING
    // =====================================================


    private void LoadJSON()
    {
        if (clipJson == null)
        {
            Debug.LogWarning(
                "SignPlaybackController: No clipJson assigned in the Inspector. " +
                "Waiting for PlaySign() call or assign a JSON file to clipJson."
            );
            return;
        }

        try
        {
            activeSignId = clipJson.name.ToLowerInvariant();
            data = JsonUtility.FromJson<LandmarkFile>(
                clipJson.text
            );

            if (data == null ||
                data.frames == null ||
                data.frames.Length == 0)
            {
                Debug.LogError(
                    "SignPlaybackController: " +
                    "JSON could not be loaded " +
                    "or contains no frames."
                );

                return;
            }

            ConfigureLandmarkAspectRatio();


            // Use the video's FPS if available.
            if (data.video != null &&
                data.video.fps > 0)
            {
                frameDuration =
                    1f / data.video.fps;
            }


            Debug.Log(
                "SignPlaybackController: Loaded " +
                data.frames.Length +
                " frames at " +
                (1f / frameDuration) +
                " FPS."
            );
        }
        catch (Exception error)
        {
            Debug.LogError(
                "SignPlaybackController JSON error: " +
                error.Message
            );
        }
    }

    private void DispatchCurrentFrame()
    {
        if (data == null || data.frames == null || data.frames.Length == 0)
            return;

        FrameData current = data.frames[currentFrame];
        if (!interpolateLandmarks || frameDuration <= 0f || data.frames.Length < 2)
        {
            OnFrameUpdate.Invoke(current);
            return;
        }

        int nextIndex = currentFrame + 1;
        if (nextIndex >= data.frames.Length)
            nextIndex = loop ? 0 : currentFrame;

        FrameData next = data.frames[nextIndex];
        float amount = Mathf.Clamp01(frameTimer / frameDuration);
        if (interpolatedFrame == null)
            interpolatedFrame = new FrameData();

        interpolatedFrame.frame = current.frame;
        interpolatedFrame.timestamp = Mathf.Lerp(current.timestamp, next.timestamp, amount);
        interpolatedFrame.pose = InterpolateLandmarks(current.pose, next.pose, amount, interpolatedFrame.pose);
        interpolatedFrame.pose_world = InterpolateLandmarks(current.pose_world, next.pose_world, amount, interpolatedFrame.pose_world);
        interpolatedFrame.left_hand = InterpolateLandmarks(current.left_hand, next.left_hand, amount, interpolatedFrame.left_hand);
        interpolatedFrame.right_hand = InterpolateLandmarks(current.right_hand, next.right_hand, amount, interpolatedFrame.right_hand);
        // Face blendshapes are name-mapped by FaceController; retain the current
        // frame because the arm and hand motion is the source of the visible stepping.
        interpolatedFrame.face_blendshapes = current.face_blendshapes;

        OnFrameUpdate.Invoke(interpolatedFrame);
    }

    private static Landmark[] InterpolateLandmarks(
        Landmark[] from,
        Landmark[] to,
        float amount,
        Landmark[] reusable)
    {
        if (from == null || to == null || from.Length != to.Length)
            return amount < 0.5f ? from : to;

        if (reusable == null || reusable.Length != from.Length)
        {
            reusable = new Landmark[from.Length];
            for (int i = 0; i < reusable.Length; i++)
                reusable[i] = new Landmark();
        }

        for (int i = 0; i < from.Length; i++)
        {
            if (from[i] == null || to[i] == null)
            {
                reusable[i] = amount < 0.5f ? from[i] : to[i];
                continue;
            }

            if (reusable[i] == null)
                reusable[i] = new Landmark();

            reusable[i].x = Mathf.Lerp(from[i].x, to[i].x, amount);
            reusable[i].y = Mathf.Lerp(from[i].y, to[i].y, amount);
            reusable[i].z = Mathf.Lerp(from[i].z, to[i].z, amount);
            reusable[i].visibility = Mathf.Lerp(from[i].visibility, to[i].visibility, amount);
            reusable[i].presence = Mathf.Lerp(from[i].presence, to[i].presence, amount);
        }

        return reusable;
    }

    private void ConfigureLandmarkAspectRatio()
    {
        if (data == null ||
            data.video == null ||
            data.video.width <= 0 ||
            data.video.height <= 0)
        {
            return;
        }

        // MediaPipe normalizes X by frame width and Y by frame height.
        // Convert X into the same units as Y before computing arm/finger
        // directions; the reciprocal stretches narrow portrait clips.
        float horizontalScale =
            (float)data.video.width / data.video.height;

        BodyController body = GetComponentInChildren<BodyController>();
        if (body == null)
        {
            body = GetComponentInParent<BodyController>();
        }

        if (body != null)
        {
            body.landmarkHorizontalScale = horizontalScale;
            body.activeSignId = activeSignId;
        }

        bool mirrorHandLandmarks = body != null && body.mirrorX;

        HandController[] handControllers =
            GetComponentsInChildren<HandController>();

        foreach (HandController handController in handControllers)
        {
            if (handController != null && handController.enabled)
            {
                handController.landmarkHorizontalScale = horizontalScale;
                handController.mirrorX = mirrorHandLandmarks;
            }
        }
    }


    // =====================================================
    // PUBLIC API — TRANSPORT CONTROLS
    // =====================================================


    /// <summary>
    /// Begin (or resume) playback from the current frame.
    /// If the JSON hasn't been loaded yet, loads it first.
    /// </summary>
    public void Play()
    {
        if (data == null)
        {
            LoadJSON();
        }

        if (data == null ||
            data.frames == null)
            return;

        playing = true;
    }


    /// <summary>
    /// Pause playback without resetting the frame position.
    /// </summary>
    public void Pause()
    {
        playing = false;
    }


    /// <summary>
    /// Stop playback and reset to the beginning.
    /// </summary>
    public void Stop()
    {
        playing = false;

        currentFrame = 0;
        frameTimer = 0f;
        OnPlaybackFinished.Invoke();
    }


    /// <summary>
    /// Reset to the first frame and start playing.
    /// </summary>
    public void Restart()
    {
        currentFrame = 0;
        frameTimer = 0f;

        playing = true;
    }


    /// <summary>
    /// Loads and plays a named clip from StreamingAssets/signs/{signId}.json.
    /// The web page calls this through SignPlaybackBridge in the WebGL build.
    /// </summary>
    public void PlaySign(string signId)
    {
        string normalizedId = (signId ?? string.Empty).Trim().ToLowerInvariant();
        if (normalizedId.Length == 0)
        {
            Debug.LogWarning("SignPlaybackController: A sign name is required.");
            return;
        }

        for (int i = 0; i < normalizedId.Length; i++)
        {
            char c = normalizedId[i];
            if (!(char.IsLetterOrDigit(c) || c == '-'))
            {
                Debug.LogWarning("SignPlaybackController: Invalid sign name: " + signId);
                return;
            }
        }

        signLoadVersion++;
        activeSignId = normalizedId;
        playing = false;
        StartCoroutine(LoadAndPlaySign(normalizedId, signLoadVersion));
    }


    public void SetPlaybackSpeed(float speed)
    {
        playbackSpeed = Mathf.Clamp(speed, 0.25f, 3f);
    }


    private IEnumerator LoadAndPlaySign(string signId, int requestVersion)
    {
        string url = Application.streamingAssetsPath + "/signs/" + signId + ".json";

#if UNITY_EDITOR && !UNITY_WEBGL
        // In the Unity Editor on Windows, file:// protocol is needed for local files
        if (!url.Contains("://"))
        {
            url = "file:///" + url.Replace("\\", "/");
        }
#endif

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (requestVersion != signLoadVersion)
            {
                yield break;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(
                    "SignPlaybackController: Could not load sign '" + signId +
                    "' from " + url + ": " + request.error
                );
                yield break;
            }

            LandmarkFile loadedData = JsonUtility.FromJson<LandmarkFile>(request.downloadHandler.text);
            if (loadedData == null || loadedData.frames == null || loadedData.frames.Length == 0)
            {
                Debug.LogError("SignPlaybackController: Sign data is empty or invalid: " + signId);
                yield break;
            }

            data = loadedData;
            ConfigureLandmarkAspectRatio();

            if (data.video != null && data.video.fps > 0f)
            {
                frameDuration = 1f / data.video.fps;
            }

            currentFrame = 0;
            frameTimer = 0f;
            playing = true;
            DispatchCurrentFrame();

            Debug.Log("SignPlaybackController: Playing sign '" + signId + "'.");
        }
    }


    /// <summary>
    /// Jump to a specific frame index (clamped to valid
    /// range) and immediately dispatch it to listeners.
    /// </summary>
    public void SetFrame(int frame)
    {
        if (data == null ||
            data.frames == null ||
            data.frames.Length == 0)
            return;

        currentFrame =
            Mathf.Clamp(
                frame,
                0,
                data.frames.Length - 1
            );

        frameTimer = 0f;

        // Immediately broadcast the new frame so
        // listeners update even while paused.
        DispatchCurrentFrame();
    }


    // =====================================================
    // PUBLIC API — QUERIES
    // =====================================================


    /// <summary>
    /// Returns the current frame index.
    /// </summary>
    public int GetCurrentFrame()
    {
        return currentFrame;
    }


    /// <summary>
    /// Returns the total number of frames in the clip.
    /// </summary>
    public int GetTotalFrames()
    {
        if (data == null ||
            data.frames == null)
            return 0;

        return data.frames.Length;
    }


    /// <summary>
    /// Returns normalised playback progress (0 → 1).
    /// </summary>
    public float GetProgress()
    {
        int total = GetTotalFrames();

        if (total <= 1)
            return 0f;

        return (float)currentFrame /
               (float)(total - 1);
    }


    /// <summary>
    /// True while the controller is actively playing.
    /// </summary>
    public bool IsPlaying => playing;


    /// <summary>
    /// Returns the deserialized LandmarkFile (or null
    /// if not yet loaded).
    /// </summary>
    public LandmarkFile GetData()
    {
        return data;
    }
}
