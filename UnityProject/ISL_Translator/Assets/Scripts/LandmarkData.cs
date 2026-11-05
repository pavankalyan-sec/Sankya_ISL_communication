using System;
using UnityEngine;


// =========================================================
// SHARED DATA MODEL
//
// Serializable classes for deserializing MediaPipe
// Holistic Landmarks JSON.
//
// Used by BodyController, HandController, and any other
// script that needs to read landmark data.
// =========================================================


// ---------------------------------------------------------
// Single landmark point
// ---------------------------------------------------------

[Serializable]
public class Landmark
{
    public float x;
    public float y;
    public float z;
    public float visibility;
    public float presence;
}


// ---------------------------------------------------------
// ARKit face blendshape entry
// ---------------------------------------------------------

[Serializable]
public class BlendshapeData
{
    public string name;
    public float score;
}


// ---------------------------------------------------------
// One frame of captured data
// ---------------------------------------------------------

[Serializable]
public class FrameData
{
    public int frame;
    public float timestamp;

    // 33 pose landmarks (normalized 0-1 coords)
    public Landmark[] pose;

    // 33 pose landmarks (real-world meters)
    public Landmark[] pose_world;

    // 21 left hand landmarks
    public Landmark[] left_hand;

    // 21 right hand landmarks
    public Landmark[] right_hand;

    // 52 ARKit face blendshapes
    public BlendshapeData[] face_blendshapes;
}


// ---------------------------------------------------------
// Video metadata
// ---------------------------------------------------------

[Serializable]
public class VideoData
{
    public string filename;
    public int width;
    public int height;
    public float fps;
    public int total_frames;
    public float duration;
    public int processed_frames;
}


// ---------------------------------------------------------
// Landmark counts per category
// ---------------------------------------------------------

[Serializable]
public class LandmarkCounts
{
    public int pose;
    public int left_hand;
    public int right_hand;
}


// ---------------------------------------------------------
// Root object for the entire JSON file
// ---------------------------------------------------------

[Serializable]
public class LandmarkFile
{
    public string format;
    public int version;
    public VideoData video;
    public LandmarkCounts landmark_counts;
    public FrameData[] frames;
}


// =========================================================
// UTILITY HELPERS
//
// Static methods for converting MediaPipe coordinate
// systems into Unity coordinate systems.
// =========================================================

public static class LandmarkUtils
{
    // -----------------------------------------------------
    // MediaPipeToUnity
    //
    // Converts normalized (0-1) MediaPipe pose landmarks
    // into Unity world-space coordinates.
    //
    //   x → (x - 0.5)   center-origin
    //   y → (0.5 - y)    flip vertical
    //   z → -z           flip depth
    //
    // Optional mirror flips the x axis.
    // Optional scale multiplies all axes.
    // -----------------------------------------------------

    public static Vector3 MediaPipeToUnity(
        Landmark lm,
        float scale = 1f,
        bool mirror = false,
        float horizontalScale = 1f
    )
    {
        if (lm == null)
            return Vector3.zero;


        // MediaPipe normalizes x by image width and y by image height.
        // Correct x for the source aspect ratio before using both axes
        // as spatial coordinates.
        float x =
            (lm.x - 0.5f) * horizontalScale;

        float y =
            0.5f - lm.y;

        float z =
            -lm.z * horizontalScale;


        if (mirror)
        {
            x = -x;
        }


        return new Vector3(
            x * scale,
            y * scale,
            z * scale
        );
    }


    // -----------------------------------------------------
    // WorldToUnity
    //
    // Converts pose_world landmarks (already in meters)
    // into Unity world-space coordinates.
    //
    //   x →  x
    //   y → -y    flip vertical
    //   z → -z    flip depth
    // -----------------------------------------------------

    public static Vector3 WorldToUnity(
        Landmark lm,
        float scale = 1f,
        bool mirror = false
    )
    {
        if (lm == null)
            return Vector3.zero;


        float x = mirror ? -lm.x : lm.x;

        return new Vector3(x, -lm.y, -lm.z) * scale;
    }


    // -----------------------------------------------------
    // HandToUnity
    //
    // Converts hand landmark coordinates into Unity
    // world-space. Same convention as WorldToUnity.
    //
    //   x →  x
    //   y → -y    flip vertical
    //   z → -z    flip depth
    // -----------------------------------------------------

    public static Vector3 HandToUnity(
        Landmark lm,
        float horizontalScale = 1f,
        bool mirror = false
    )
    {
        if (lm == null)
            return Vector3.zero;


        return new Vector3(
            (mirror ? -lm.x : lm.x) * horizontalScale,
            -lm.y,
            -lm.z * horizontalScale
        );
    }
}
