using UnityEngine;

public class HandRigTest : MonoBehaviour
{
    public Transform index02;
    public Transform index03;

    public Transform middle02;
    public Transform middle03;

    public Transform ring02;
    public Transform ring03;

    public Transform pinky02;
    public Transform pinky03;

    public Transform thumb02;
    public Transform thumb03;

    public float bendAngle = 45f;
    public float speed = 2f;

    void Update()
    {
        float angle = Mathf.Sin(Time.time * speed) * bendAngle;

        Bend(index02, angle);
        Bend(index03, angle);

        Bend(middle02, angle);
        Bend(middle03, angle);

        Bend(ring02, angle);
        Bend(ring03, angle);

        Bend(pinky02, angle);
        Bend(pinky03, angle);

        Bend(thumb02, angle);
        Bend(thumb03, angle);
    }

    void Bend(Transform bone, float angle)
    {
        if (bone == null)
            return;

        bone.localRotation = Quaternion.Euler(
            angle,
            0f,
            0f
        );
    }
}