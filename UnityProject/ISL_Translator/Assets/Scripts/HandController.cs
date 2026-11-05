using UnityEngine;


public class HandController : MonoBehaviour
{
    [Header("Left Hand")]
    public Transform leftThumb1;
    public Transform leftThumb2;
    public Transform leftThumb3;

    public Transform leftIndex1;
    public Transform leftIndex2;
    public Transform leftIndex3;

    public Transform leftMiddle1;
    public Transform leftMiddle2;
    public Transform leftMiddle3;

    public Transform leftRing1;
    public Transform leftRing2;
    public Transform leftRing3;

    public Transform leftPinky1;
    public Transform leftPinky2;
    public Transform leftPinky3;

    [Header("Right Hand")]
    public Transform rightThumb1;
    public Transform rightThumb2;
    public Transform rightThumb3;

    public Transform rightIndex1;
    public Transform rightIndex2;
    public Transform rightIndex3;

    public Transform rightMiddle1;
    public Transform rightMiddle2;
    public Transform rightMiddle3;

    public Transform rightRing1;
    public Transform rightRing2;
    public Transform rightRing3;

    public Transform rightPinky1;
    public Transform rightPinky2;
    public Transform rightPinky3;

    [Header("Settings")]
    public float smooth = 12f;

    [Tooltip("Set from the source video's width divided by height to correct normalized horizontal landmarks.")]
    [Min(0.01f)]
    public float landmarkHorizontalScale = 1f;

    [Tooltip("Match the BodyController horizontal mirroring setting.")]
    public bool mirrorX;

    [Min(0f)]
    public float handLossGracePeriod = 0.12f;

    [Range(0.1f, 2f)]
    public float fingerStrength = 1f;

    [Range(0f, 1f)]
    public float splayStrength = 0.5f;

    [Header("Anatomical Joint Limits")]
    [Tooltip("Maximum backward hyperextension allowed (degrees)")]
    [Range(0f, 15f)]
    public float maxHyperextension = 5f;

    [Tooltip("Maximum inward finger curl allowed (degrees)")]
    [Range(70f, 120f)]
    public float maxCurl = 100f;

    [Tooltip("Maximum sideways finger spread (degrees)")]
    [Range(5f, 35f)]
    public float maxSplay = 22f;


    // Original rotations of every finger bone
    private Quaternion[] originalRotations = new Quaternion[30];

    private Transform[] bones = new Transform[30];
    // Hand landmarks are consumed immediately, so reuse this scratch buffer
    // instead of allocating a new Vector3 array for every playback update.
    private readonly Vector3[] handPositions = new Vector3[21];
    private float leftHandMissingTime;
    private float rightHandMissingTime;
    private bool returningToRestPose;
    [Min(0.1f)]
    public float restPoseReturnSpeed = 8f;

    public bool TryGetRestPalmRotation(
        bool left,
        Transform wrist,
        out Quaternion palmRotation
    )
    {
        Transform index = left ? leftIndex1 : rightIndex1;
        Transform middle = left ? leftMiddle1 : rightMiddle1;
        Transform pinky = left ? leftPinky1 : rightPinky1;

        palmRotation = Quaternion.identity;

        if (wrist == null || index == null || middle == null || pinky == null)
        {
            return false;
        }

        Vector3 palmForward = (middle.position - wrist.position).normalized;
        Vector3 palmSide = (pinky.position - wrist.position).normalized;
        Vector3 palmNormal = Vector3.Cross(palmForward, palmSide).normalized;

        if (palmForward.sqrMagnitude < 0.000001f ||
            palmNormal.sqrMagnitude < 0.000001f)
        {
            return false;
        }

        palmRotation = Quaternion.LookRotation(palmForward, palmNormal);
        return true;
    }


    void Start()
    {
        SetupBones();

        SaveOriginalRotations();
    }

    private void Update()
    {
        if (!returningToRestPose)
        {
            return;
        }

        float amount = 1f - Mathf.Exp(-restPoseReturnSpeed * Time.deltaTime);
        bool settled = true;
        for (int i = 0; i < bones.Length; i++)
        {
            if (bones[i] == null)
            {
                continue;
            }

            bones[i].localRotation = Quaternion.Slerp(
                bones[i].localRotation,
                originalRotations[i],
                amount
            );

            if (Quaternion.Angle(bones[i].localRotation, originalRotations[i]) > 0.25f)
            {
                settled = false;
            }
        }

        if (settled)
        {
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] != null)
                {
                    bones[i].localRotation = originalRotations[i];
                }
            }

            returningToRestPose = false;
        }
    }

    public void ReturnToRestPose()
    {
        returningToRestPose = true;
        leftHandMissingTime = 0f;
        rightHandMissingTime = 0f;
    }


    // =========================================================
    // BONE SETUP
    // =========================================================

    void SetupBones()
    {
        // LEFT

        bones[0] = leftThumb1;
        bones[1] = leftThumb2;
        bones[2] = leftThumb3;

        bones[3] = leftIndex1;
        bones[4] = leftIndex2;
        bones[5] = leftIndex3;

        bones[6] = leftMiddle1;
        bones[7] = leftMiddle2;
        bones[8] = leftMiddle3;

        bones[9] = leftRing1;
        bones[10] = leftRing2;
        bones[11] = leftRing3;

        bones[12] = leftPinky1;
        bones[13] = leftPinky2;
        bones[14] = leftPinky3;


        // RIGHT

        bones[15] = rightThumb1;
        bones[16] = rightThumb2;
        bones[17] = rightThumb3;

        bones[18] = rightIndex1;
        bones[19] = rightIndex2;
        bones[20] = rightIndex3;

        bones[21] = rightMiddle1;
        bones[22] = rightMiddle2;
        bones[23] = rightMiddle3;

        bones[24] = rightRing1;
        bones[25] = rightRing2;
        bones[26] = rightRing3;

        bones[27] = rightPinky1;
        bones[28] = rightPinky2;
        bones[29] = rightPinky3;
    }


    // =========================================================
    // SAVE ORIGINAL ROTATIONS
    // =========================================================

    void SaveOriginalRotations()
    {
        for (int i = 0; i < bones.Length; i++)
        {
            if (bones[i] != null)
            {
                originalRotations[i] =
                    bones[i].localRotation;
            }
        }
    }


    // =========================================================
    // PUBLIC API
    //
    // Called by SignPlaybackController via UnityEvent.
    // Receives a single FrameData and applies both hands.
    // =========================================================

    public void ApplyFrame(FrameData frame)
    {
        if (frame == null)
            return;

        returningToRestPose = false;


        if (frame.left_hand != null &&
            frame.left_hand.Length >= 21)
        {
            leftHandMissingTime = 0f;
            ApplyHand(
                frame.left_hand,
                true
            );
        }
        else
        {
            leftHandMissingTime += Time.deltaTime;
            if (leftHandMissingTime >= handLossGracePeriod)
            {
                RelaxHand(true);
            }
        }

        if (frame.right_hand != null &&
            frame.right_hand.Length >= 21)
        {
            rightHandMissingTime = 0f;
            ApplyHand(
                frame.right_hand,
                false
            );
        }
        else
        {
            rightHandMissingTime += Time.deltaTime;
            if (rightHandMissingTime >= handLossGracePeriod)
            {
                RelaxHand(false);
            }
        }
    }

    private void RelaxHand(bool left)
    {
        int firstBone = left ? 0 : 15;
        int lastBone = firstBone + 15;
        float interpolation =
            1f - Mathf.Exp(-smooth * Time.deltaTime);

        for (int i = firstBone; i < lastBone; i++)
        {
            if (bones[i] == null)
            {
                continue;
            }

            bones[i].localRotation = Quaternion.Slerp(
                bones[i].localRotation,
                originalRotations[i],
                interpolation
            );
        }
    }


    // =========================================================
    // APPLY HAND
    // =========================================================

    void ApplyHand(
        Landmark[] h,
        bool left
    )
    {
        for (int i = 0; i < 21; i++)
        {
            handPositions[i] = LandmarkUtils.HandToUnity(
                h[i],
                landmarkHorizontalScale,
                mirrorX
            );
        }

        Vector3[] p = handPositions;


        // -------------------------------------------------
        // Palm normal
        //
        // Computed from wrist, index MCP, and pinky MCP.
        // Used as the reference plane for splay angles.
        // -------------------------------------------------

        Vector3 wrist = p[0];
        Vector3 indexMCP = p[5];
        Vector3 pinkyMCP = p[17];

        Vector3 palmForward =
            (indexMCP - wrist).normalized;

        Vector3 palmSide =
            (pinkyMCP - wrist).normalized;

        Vector3 palmNormal =
            Vector3.Cross(
                palmForward,
                palmSide
            ).normalized;


        // -------------------------------------------------
        // Middle finger reference direction
        //
        // All finger splay angles are measured relative
        // to the middle finger MCP → PIP direction.
        // -------------------------------------------------

        Vector3 middleDir =
            (p[10] - p[9]).normalized;


        // =====================================================
        // INDEX
        // =====================================================

        Vector3 indexDir =
            (p[6] - p[5]).normalized;

        float indexSplay =
            Vector3.SignedAngle(
                middleDir,
                indexDir,
                palmNormal
            );

        ApplyFinger(
            left ? leftIndex1 : rightIndex1,
            left ? leftIndex2 : rightIndex2,
            left ? leftIndex3 : rightIndex3,
            p[5],
            p[6],
            p[7],
            p[8],
            indexSplay
        );


        // =====================================================
        // MIDDLE
        // =====================================================

        // Middle finger is the splay reference — splay = 0

        ApplyFinger(
            left ? leftMiddle1 : rightMiddle1,
            left ? leftMiddle2 : rightMiddle2,
            left ? leftMiddle3 : rightMiddle3,
            p[9],
            p[10],
            p[11],
            p[12],
            0f
        );


        // =====================================================
        // RING
        // =====================================================

        Vector3 ringDir =
            (p[14] - p[13]).normalized;

        float ringSplay =
            Vector3.SignedAngle(
                middleDir,
                ringDir,
                palmNormal
            );

        ApplyFinger(
            left ? leftRing1 : rightRing1,
            left ? leftRing2 : rightRing2,
            left ? leftRing3 : rightRing3,
            p[13],
            p[14],
            p[15],
            p[16],
            ringSplay
        );


        // =====================================================
        // PINKY
        // =====================================================

        Vector3 pinkyDir =
            (p[18] - p[17]).normalized;

        float pinkySplay =
            Vector3.SignedAngle(
                middleDir,
                pinkyDir,
                palmNormal
            );

        ApplyFinger(
            left ? leftPinky1 : rightPinky1,
            left ? leftPinky2 : rightPinky2,
            left ? leftPinky3 : rightPinky3,
            p[17],
            p[18],
            p[19],
            p[20],
            pinkySplay
        );


        // =====================================================
        // THUMB (special handling)
        //
        // Thumb splay is measured between thumb direction
        // (p[2]-p[1]) and index direction (p[6]-p[5]),
        // projected onto the palm plane.
        //
        // The first thumb bone applies splay on local Z
        // instead of local X.
        // =====================================================

        Vector3 thumbDir =
            (p[2] - p[1]).normalized;

        float thumbSplay =
            Vector3.SignedAngle(
                indexDir,
                thumbDir,
                palmNormal
            );

        ApplyThumb(
            left ? leftThumb1 : rightThumb1,
            left ? leftThumb2 : rightThumb2,
            left ? leftThumb3 : rightThumb3,
            p[1],
            p[2],
            p[3],
            p[4],
            thumbSplay
        );
    }


    // =========================================================
    // FINGER BEND + SPLAY
    //
    // Standard finger: bend on local X, splay on local Z.
    // Splay is only applied to the first bone (MCP joint).
    // =========================================================

    void ApplyFinger(
        Transform bone1,
        Transform bone2,
        Transform bone3,

        Vector3 p0,
        Vector3 p1,
        Vector3 p2,
        Vector3 p3,

        float splay
    )
    {
        if (bone1 == null ||
            bone2 == null ||
            bone3 == null)
            return;


        // MediaPipe finger segments

        Vector3 a =
            (p1 - p0).normalized;

        Vector3 b =
            (p2 - p1).normalized;

        Vector3 c =
            (p3 - p2).normalized;


        // Calculate bend angles

        float angle1 =
            Vector3.Angle(a, b);

        float angle2 =
            Vector3.Angle(b, c);


        // -------------------------------------------------
        // IMPORTANT
        //
        // Bend rotates around the LOCAL X axis.
        // Splay rotates around the LOCAL Z axis.
        //
        // Splay is only applied to the first bone
        // (MCP joint) to spread the finger.
        // -------------------------------------------------

        ApplyBendAndSplay(
            bone1,
            angle1,
            splay
        );

        ApplyBend(
            bone2,
            angle2
        );


        // Third segment follows the last joint.
        ApplyBend(
            bone3,
            angle2 * 0.5f
        );
    }


    // =========================================================
    // THUMB BEND + SPLAY
    //
    // The thumb has a different joint structure.
    // Its splay is applied on local Z for the first bone.
    // =========================================================

    void ApplyThumb(
        Transform bone1,
        Transform bone2,
        Transform bone3,

        Vector3 p0,
        Vector3 p1,
        Vector3 p2,
        Vector3 p3,

        float splay
    )
    {
        if (bone1 == null ||
            bone2 == null ||
            bone3 == null)
            return;


        // MediaPipe thumb segments

        Vector3 a =
            (p1 - p0).normalized;

        Vector3 b =
            (p2 - p1).normalized;

        Vector3 c =
            (p3 - p2).normalized;


        // Calculate bend angles

        float angle1 =
            Vector3.Angle(a, b);

        float angle2 =
            Vector3.Angle(b, c);


        // -------------------------------------------------
        // Thumb first bone:
        //   Splay on local Z (spread away from palm)
        //   Bend on local X (curl)
        // -------------------------------------------------

        ApplyBendAndSplay(
            bone1,
            angle1,
            splay
        );

        ApplyBend(
            bone2,
            angle2
        );


        // Third segment follows the last joint.
        ApplyBend(
            bone3,
            angle2 * 0.5f
        );
    }


    // =========================================================
    // APPLY LOCAL BEND + SPLAY
    //
    // Combines bend (local X) and splay (local Z) into a
    // single target rotation for the MCP bone.
    // =========================================================

    void ApplyBendAndSplay(
        Transform bone,
        float bendAngle,
        float splayAngle
    )
    {
        if (bone == null)
            return;


        int index =
            GetBoneIndex(bone);

        if (index < 0)
            return;


        // Seed-san starts straight.
        // MediaPipe angle represents the amount
        // the finger is curling.

        float bend =
            Mathf.Clamp(
                bendAngle * fingerStrength,
                -maxHyperextension,
                maxCurl
            );

        float splay =
            Mathf.Clamp(
                splayAngle * splayStrength,
                -maxSplay,
                maxSplay
            );


        // Combine bend (local X) and splay (local Z)
        // on top of the original resting rotation.

        Quaternion target =
            originalRotations[index] *
            Quaternion.AngleAxis(
                splay,
                Vector3.forward
            ) *
            Quaternion.AngleAxis(
                bend,
                Vector3.right
            );


        float t =
            1f -
            Mathf.Exp(
                -smooth *
                Time.deltaTime
            );


        bone.localRotation =
            Quaternion.Slerp(
                bone.localRotation,
                target,
                t
            );
    }


    // =========================================================
    // APPLY LOCAL BEND
    // =========================================================

    void ApplyBend(
        Transform bone,
        float angle
    )
    {
        if (bone == null)
            return;


        int index =
            GetBoneIndex(bone);

        if (index < 0)
            return;


        // Seed-san starts straight.
        // MediaPipe angle represents the amount
        // the finger is curling.

        float bend =
            Mathf.Clamp(
                angle * fingerStrength,
                -maxHyperextension,
                maxCurl
            );


        // Seed-san's finger bend axis.
        //
        // We deliberately use LOCAL X.
        // No world-space X/Y/Z rotation.

        Quaternion target =
            originalRotations[index] *
            Quaternion.AngleAxis(
                bend,
                Vector3.right
            );


        float t =
            1f -
            Mathf.Exp(
                -smooth *
                Time.deltaTime
            );


        bone.localRotation =
            Quaternion.Slerp(
                bone.localRotation,
                target,
                t
            );
    }


    // =========================================================
    // FIND BONE INDEX
    // =========================================================

    int GetBoneIndex(
        Transform bone
    )
    {
        for (int i = 0; i < bones.Length; i++)
        {
            if (bones[i] == bone)
                return i;
        }

        return -1;
    }
}
