using System;
using System.Collections.Generic;
using UnityEngine;
using UniVRM10;


// =========================================================
// FACE CONTROLLER
//
// Applies MediaPipe ARKit face blendshapes to a VRM 1.0
// avatar using UniVRM's runtime expression system.
//
// MediaPipe outputs 52 ARKit-style blendshape names.
// VRM 1.0 has ~20 built-in expression presets.
// This script maps the most important ARKit shapes to
// their closest VRM equivalents.
//
// Called by SignPlaybackController via OnFrameUpdate.
//
// Uses shared types from LandmarkData.cs:
//   FrameData, BlendshapeData
// =========================================================


public class FaceController : MonoBehaviour
{
    // =====================================================
    // INSPECTOR FIELDS
    // =====================================================

    [Header("VRM")]
    [Tooltip("Leave empty to auto-find Vrm10Instance on this GameObject or children.")]
    public Vrm10Instance vrmInstance;


    [Header("Settings")]
    [Range(0f, 2f)]
    public float expressionStrength = 1f;

    [Range(1f, 30f)]
    public float smooth = 12f;


    // =====================================================
    // PRIVATE STATE
    // =====================================================

    // Current (smoothed) expression values keyed by
    // VRM expression name.
    private Dictionary<string, float> currentValues =
        new Dictionary<string, float>();

    // Target expression values for the current frame.
    private Dictionary<string, float> targetValues =
        new Dictionary<string, float>();

    private static readonly string[] NeutralExpressionKeys =
    {
        "happy", "sad", "angry", "surprised",
        "aa", "ih", "ou", "ee", "oh",
        "blink", "blinkLeft", "blinkRight"
    };

    private bool returningToNeutral;


    // =====================================================
    // UNITY LIFECYCLE
    // =====================================================


    private void Start()
    {
        // -------------------------------------------------
        // Auto-find VRM instance if not assigned
        // -------------------------------------------------

        if (vrmInstance == null)
        {
            vrmInstance =
                GetComponentInChildren<Vrm10Instance>();
        }

        if (vrmInstance == null)
        {
            Debug.LogWarning(
                "FaceController: No Vrm10Instance found. " +
                "Face expressions disabled."
            );
        }
    }

    private void Update()
    {
        if (!returningToNeutral || vrmInstance == null)
        {
            return;
        }

        var runtime = vrmInstance.Runtime;
        if (runtime == null)
        {
            returningToNeutral = false;
            return;
        }

        float amount = 1f - Mathf.Exp(-smooth * Time.deltaTime);
        bool settled = true;
        for (int i = 0; i < NeutralExpressionKeys.Length; i++)
        {
            string key = NeutralExpressionKeys[i];
            float current;
            if (!currentValues.TryGetValue(key, out current))
            {
                current = 0f;
            }

            float next = Mathf.Lerp(current, 0f, amount);
            currentValues[key] = next;
            ApplyExpression(runtime, key, next);
            if (next > 0.001f)
            {
                settled = false;
            }
        }

        if (settled)
        {
            for (int i = 0; i < NeutralExpressionKeys.Length; i++)
            {
                string key = NeutralExpressionKeys[i];
                currentValues[key] = 0f;
                ApplyExpression(runtime, key, 0f);
            }

            returningToNeutral = false;
        }
    }

    public void ReturnToNeutral()
    {
        returningToNeutral = vrmInstance != null;
    }


    // =====================================================
    // APPLY FRAME
    //
    // Called by SignPlaybackController via UnityEvent.
    // Reads ARKit blendshapes from the FrameData, maps
    // them to VRM expressions, smooths, and applies.
    // =====================================================


    public void ApplyFrame(FrameData frame)
    {
        if (vrmInstance == null)
            return;

        if (frame.face_blendshapes == null ||
            frame.face_blendshapes.Length == 0)
            return;

        returningToNeutral = false;


        // -------------------------------------------------
        // Extract MediaPipe ARKit blendshapes
        // -------------------------------------------------

        // Eyes

        float blinkL =
            GetBlendshape(frame, "eyeBlinkLeft");

        float blinkR =
            GetBlendshape(frame, "eyeBlinkRight");


        // Mouth & Visemes

        float jawOpen =
            GetBlendshape(frame, "jawOpen");

        float mouthSmileL =
            GetBlendshape(frame, "mouthSmileLeft");

        float mouthSmileR =
            GetBlendshape(frame, "mouthSmileRight");

        float mouthFrownL =
            GetBlendshape(frame, "mouthFrownLeft");

        float mouthFrownR =
            GetBlendshape(frame, "mouthFrownRight");

        float mouthPucker =
            GetBlendshape(frame, "mouthPucker");

        float mouthFunnel =
            GetBlendshape(frame, "mouthFunnel");

        float mouthStretchL =
            GetBlendshape(frame, "mouthStretchLeft");

        float mouthStretchR =
            GetBlendshape(frame, "mouthStretchRight");


        // Brows

        float browDownL =
            GetBlendshape(frame, "browDownLeft");

        float browDownR =
            GetBlendshape(frame, "browDownRight");

        float browUp =
            GetBlendshape(frame, "browInnerUp");


        // -------------------------------------------------
        // Derive VRM expression values
        //
        //   happy     ← average of mouth smile L/R
        //   sad       ← average of mouth frown L/R
        //   angry     ← average of brow down L/R
        //   surprised ← brow inner up
        //   aa        ← jaw open
        //   ou        ← mouth pucker
        // -------------------------------------------------

        float happy =
            (mouthSmileL + mouthSmileR) * 0.5f;

        float sad =
            (mouthFrownL + mouthFrownR) * 0.5f;

        float angry =
            (browDownL + browDownR) * 0.5f;

        float surprised =
            browUp;

        // ISL Mouthing Visemes
        float aa = jawOpen;
        float ou = mouthPucker;
        float oh = Mathf.Clamp01(mouthFunnel * 0.8f + jawOpen * 0.3f);
        float stretch = (mouthStretchL + mouthStretchR) * 0.5f;
        float ee = Mathf.Clamp01(stretch * 0.9f + (mouthSmileL + mouthSmileR) * 0.3f);
        float ih = Mathf.Clamp01(stretch * 0.6f + jawOpen * 0.2f);


        // -------------------------------------------------
        // Build target values dictionary
        // -------------------------------------------------

        targetValues.Clear();

        // Emotional expressions
        targetValues["happy"]     = happy;
        targetValues["sad"]       = sad;
        targetValues["angry"]     = angry;
        targetValues["surprised"] = surprised;

        // All 5 VRM Japanese / ISL Mouthing visemes
        targetValues["aa"] = aa;
        targetValues["ih"] = ih;
        targetValues["ou"] = ou;
        targetValues["ee"] = ee;
        targetValues["oh"] = oh;

        // Blinks
        targetValues["blink"]      = (blinkL + blinkR) * 0.5f;
        targetValues["blinkLeft"]  = blinkL;
        targetValues["blinkRight"] = blinkR;


        // -------------------------------------------------
        // Smooth and apply each expression
        // -------------------------------------------------

        float t =
            1f -
            Mathf.Exp(
                -smooth *
                Time.deltaTime
            );

        var runtime =
            vrmInstance.Runtime;


        foreach (var kvp in targetValues)
        {
            string key = kvp.Key;
            float target = kvp.Value;

            // Get the current smoothed value,
            // defaulting to 0 if this is the first frame.
            float current;
            if (!currentValues.TryGetValue(key, out current))
            {
                current = 0f;
            }


            // Exponential smoothing
            float smoothed =
                Mathf.Lerp(
                    current,
                    target,
                    t
                );

            currentValues[key] = smoothed;


            // Clamp the final value to [0, 1] after
            // applying expression strength.
            float final_value =
                Mathf.Clamp01(
                    smoothed *
                    expressionStrength
                );


            // -----------------------------------------
            // Apply to VRM expression preset
            // -----------------------------------------

            ApplyExpression(
                runtime,
                key,
                final_value
            );
        }
    }


    // =====================================================
    // APPLY EXPRESSION
    //
    // Maps a string key to the corresponding VRM 1.0
    // ExpressionKey preset and sets its weight.
    // =====================================================


    private void ApplyExpression(
        Vrm10RuntimeExpression expressionRuntime,
        string key,
        float value
    )
    {
        // NOTE: Vrm10Instance.Runtime is a
        // Vrm10Runtime object whose Expression property
        // is a Vrm10RuntimeExpression. We call
        // SetWeight(ExpressionKey, float) on it.

        switch (key)
        {
            // -------------------------------------------
            // Emotional expressions
            // -------------------------------------------

            case "happy":
                expressionRuntime.SetWeight(
                    ExpressionKey.Happy,
                    value
                );
                break;

            case "sad":
                expressionRuntime.SetWeight(
                    ExpressionKey.Sad,
                    value
                );
                break;

            case "angry":
                expressionRuntime.SetWeight(
                    ExpressionKey.Angry,
                    value
                );
                break;

            case "surprised":
                expressionRuntime.SetWeight(
                    ExpressionKey.Surprised,
                    value
                );
                break;


            // -------------------------------------------
            // Mouth shapes (visemes)
            // -------------------------------------------

            case "aa":
                expressionRuntime.SetWeight(
                    ExpressionKey.Aa,
                    value
                );
                break;

            case "ou":
                expressionRuntime.SetWeight(
                    ExpressionKey.Ou,
                    value
                );
                break;


            // -------------------------------------------
            // Blinks
            // -------------------------------------------

            case "blink":
                expressionRuntime.SetWeight(
                    ExpressionKey.Blink,
                    value
                );
                break;

            case "blinkLeft":
                expressionRuntime.SetWeight(
                    ExpressionKey.BlinkLeft,
                    value
                );
                break;

            case "blinkRight":
                expressionRuntime.SetWeight(
                    ExpressionKey.BlinkRight,
                    value
                );
                break;
        }
    }


    // =====================================================
    // APPLY EXPRESSION (overload)
    //
    // Convenience wrapper that accepts the full
    // Vrm10Runtime object instead of just the
    // Expression sub-object.
    // =====================================================


    private void ApplyExpression(
        Vrm10Runtime runtime,
        string key,
        float value
    )
    {
        ApplyExpression(
            runtime.Expression,
            key,
            value
        );
    }


    // =====================================================
    // HELPER — GET BLENDSHAPE
    //
    // Searches the frame's face_blendshapes array for an
    // entry matching the given ARKit name. Returns the
    // score if found, 0 if not.
    // =====================================================


    private float GetBlendshape(
        FrameData frame,
        string name
    )
    {
        if (frame.face_blendshapes == null)
            return 0f;


        for (int i = 0; i < frame.face_blendshapes.Length; i++)
        {
            BlendshapeData bs =
                frame.face_blendshapes[i];

            if (bs == null)
                continue;

            if (bs.name == name)
            {
                return bs.score;
            }
        }


        return 0f;
    }
}
