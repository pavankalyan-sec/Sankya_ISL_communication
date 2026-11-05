using UnityEngine;

public class HandWaveTest : MonoBehaviour
{
    // Arm bones
    public Transform upperArmR;
    public Transform forearmR;
    public Transform handR;

    // Fingers
    public Transform index01;
    public Transform index02;
    public Transform index03;

    public Transform middle01;
    public Transform middle02;
    public Transform middle03;

    public Transform ring01;
    public Transform ring02;
    public Transform ring03;

    public Transform pinky01;
    public Transform pinky02;
    public Transform pinky03;

    public Transform thumb01;
    public Transform thumb02;
    public Transform thumb03;

    // Wave settings
    public float waveAngle = 25f;
    public float waveSpeed = 3f;

    // Arm raise angle
    public float armRaiseAngle = -45f;

    // Store original rotations
    private Quaternion originalUpperArm;
    private Quaternion originalForearm;
    private Quaternion originalHand;

    void Start()
    {
        if (upperArmR != null)
            originalUpperArm = upperArmR.localRotation;

        if (forearmR != null)
            originalForearm = forearmR.localRotation;

        if (handR != null)
            originalHand = handR.localRotation;
    }

    void Update()
    {
        // ==========================================
        // 1. Raise the arm
        // ==========================================

        if (upperArmR != null)
        {
            upperArmR.localRotation =
                originalUpperArm *
                Quaternion.Euler(0f, 0f, armRaiseAngle);
        }


        // ==========================================
        // 2. Bend the elbow slightly
        // ==========================================

        if (forearmR != null)
        {
            forearmR.localRotation =
                originalForearm *
                Quaternion.Euler(0f, 0f, -25f);
        }


        // ==========================================
        // 3. Wave the hand
        // ==========================================

        float wave =
            Mathf.Sin(Time.time * waveSpeed)
            * waveAngle;

        if (handR != null)
        {
            handR.localRotation =
                originalHand *
                Quaternion.Euler(0f, wave, 0f);
        }


        // ==========================================
        // 4. Slightly open the fingers
        // ==========================================

        BendFinger(index02, 20f);
        BendFinger(index03, 25f);

        BendFinger(middle02, 20f);
        BendFinger(middle03, 25f);

        BendFinger(ring02, 20f);
        BendFinger(ring03, 25f);

        BendFinger(pinky02, 20f);
        BendFinger(pinky03, 25f);

        BendFinger(thumb02, 15f);
        BendFinger(thumb03, 20f);
    }


    // ==========================================
    // Finger bending
    // ==========================================

    void BendFinger(Transform bone, float angle)
    {
        if (bone == null)
            return;

        bone.localRotation =
            Quaternion.Euler(angle, 0f, 0f);
    }
}