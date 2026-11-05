using UnityEngine;

public class SeedSanWaveTest : MonoBehaviour
{
    [Header("Bones")]
    public Transform rightUpperArm;
    public Transform rightLowerArm;
    public Transform rightHand;

    [Header("Arm")]
    public float armRaiseAngle = 35f;

    [Header("Elbow")]
    public float elbowBendAngle = 20f;

    [Header("Hand Wave")]
    public float waveAngle = 20f;
    public float waveSpeed = 3f;

    private Quaternion upperArmStart;
    private Quaternion lowerArmStart;
    private Quaternion handStart;

    void Start()
    {
        if (rightUpperArm != null)
            upperArmStart = rightUpperArm.localRotation;

        if (rightLowerArm != null)
            lowerArmStart = rightLowerArm.localRotation;

        if (rightHand != null)
            handStart = rightHand.localRotation;
    }

    void Update()
    {
        // -----------------------------------------
        // ARM
        // -----------------------------------------

        if (rightUpperArm != null)
        {
            rightUpperArm.localRotation =
                upperArmStart *
                Quaternion.Euler(armRaiseAngle, 0f, 0f);
        }


        // -----------------------------------------
        // ELBOW
        // -----------------------------------------

        if (rightLowerArm != null)
        {
            rightLowerArm.localRotation =
                lowerArmStart *
                Quaternion.Euler(elbowBendAngle, 0f, 0f);
        }


        // -----------------------------------------
        // HAND WAVE
        // -----------------------------------------

        if (rightHand != null)
        {
            float wave =
                Mathf.Sin(Time.time * waveSpeed) * waveAngle;

            rightHand.localRotation =
                handStart *
                Quaternion.Euler(0f, wave, 0f);
        }
    }
}