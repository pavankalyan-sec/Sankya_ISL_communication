using System;
using System.Collections.Generic;
using UnityEngine;

public class BodyController : MonoBehaviour
{
    // =============================================================
    // INSPECTOR FIELDS
    // =============================================================

    [Header("Left Arm")]
    public Transform leftShoulder;
    public Transform leftUpperArm;
    public Transform leftForearm;
    public Transform leftHand;

    [Header("Right Arm")]
    public Transform rightShoulder;
    public Transform rightUpperArm;
    public Transform rightForearm;
    public Transform rightHand;

    [Header("Head & Neck")]
    [Tooltip("Leave empty to automatically find Seed-san's head bone.")]
    public Transform headBone;

    [Tooltip("Leave empty to automatically find the hips bone used to anchor hand depth.")]
    public Transform hipsBone;

    [Tooltip("Leave empty to automatically find the neck bone.")]
    public Transform neckBone;

    [Header("Spine")]
    public bool enableSpineRotation = false;

    [Tooltip("Leave empty to automatically find the spine bone.")]
    public Transform spineBone;

    [Range(0f, 1f)]
    public float spineRotationStrength = 0.5f;

    [Header("Shoulders")]
    public bool enableShoulderRaise = false;

    [Range(0f, 0.1f)]
    public float shoulderRaiseScale = 0.03f;

    [Header("Wrist & Forearm Twist")]
    [Range(0f, 1f)]
    public float wristRotationStrength = 0.8f;

    [Tooltip("Share palm rotation with the forearm so the wrist does not appear to bend against a rigid forearm.")]
    [Range(0f, 1f)]
    public float forearmTwistStrength = 0.55f;

    [Tooltip("Hide mesh sections whose material is named for the backpack, without modifying the imported avatar asset.")]
    public bool removeBackpackGeometry = true;

    [Tooltip("Hide Seed-san's separate robotic left-arm mesh during playback.")]
    public bool removeRoboticLeftArmGeometry = true;

    [Min(0f)]
    public float wristLossGracePeriod = 0.12f;

    [Header("Head Movement")]
    [Tooltip("How strongly the MediaPipe head movement is reproduced.")]
    [Range(0f, 1f)]
    public float headMovementAmount = 0.35f;

    [Tooltip("Maximum head rotation in degrees.")]
    [Range(0f, 30f)]
    public float maxHeadRotation = 12f;

    [Header("Spatial Distance & IK Metrics")]
    [Tooltip("Enable spatial metrics (hand-to-head, hand-to-chest, hand-to-hand)")]
    public bool enableSpatialMetrics = true;

    [Tooltip("Weight of spatial metric targets vs pure directional angle rotation")]
    [Range(0f, 1f)]
    public float spatialTargetWeight = 1f;

    [Header("Elbow Clearance")]
    [Tooltip("Move each shoulder joint slightly outward so the upper arm does not start inside the torso.")]
    [Min(0f)]
    public float shoulderOutwardOffset = 0.035f;

    [Tooltip("Minimum outward component for the elbow bend direction. Higher values keep elbows farther from the torso.")]
    [Range(0f, 0.75f)]
    public float elbowOutwardBias = 0.25f;

    [Tooltip("Scale factor for reaching spatial targets")]
    [Range(0.5f, 2f)]
    public float spatialReachScale = 1.0f;

    [Tooltip("Scales captured depth movement for every sign. Values above 1 make forward reaches and pushes more visible.")]
    [Range(0.1f, 2f)]
    public float spatialDepthScale = 1.5f;

    [Tooltip("Minimum distance the wrist target stays in front of the torso.")]
    [Range(0f, 0.35f)]
    public float minimumHandForwardClearance = 0.14f;

    [Tooltip("Small forward reach adjustment to open the elbow and keep the hand clear of the body.")]
    [Range(0f, 0.1f)]
    public float handForwardReachOffset = 0.025f;

    [Tooltip("Minimum sideways wrist offset for low hands, preventing a collapsed V-shaped starting pose.")]
    [Range(0f, 0.3f)]
    public float minimumLowHandSideOffset = 0.2f;

    [Tooltip("Upward calibration offset so fist/fingertip gestures (like salutes) reach temple/forehead")]
    [Range(0f, 0.3f)]
    public float handHeightBoost = 0.25f;

    [Tooltip("Additional height for the right hand near the face.")]
    [Range(0f, 0.3f)]
    public float rightHandFaceHeightBoost = 0.1f;

    [Tooltip("Prevent hands from crossing or penetrating too close together")]
    public bool enforceHandSeparation = true;

    [Range(0.05f, 0.3f)]
    public float minHandDistance = 0.12f;

    [Tooltip("Minimum wrist target gap for intentional two-hand contact poses, preventing overlap while allowing the hands to meet.")]
    [Range(0.03f, 0.12f)]
    public float minContactHandDistance = 0.07f;

    [Header("Settings")]
    public bool mirrorX = false;

    [Tooltip("Set from the source video's width divided by height to correct normalized horizontal landmarks.")]
    [Min(0.01f)]
    public float landmarkHorizontalScale = 1f;

    [Range(1f, 30f)]
    public float rotationSmooth = 12f;

    [Range(0.1f, 2f)]
    public float movementScale = 1f;

    [Header("Debug")]
    public bool showDebugLines = false;


    // =============================================================
    // PRIVATE STATE — Initial Bone Rotations
    // =============================================================

    // --- Left arm ---

    private Quaternion leftUpperArmInitialRotation;
    private Quaternion leftForearmInitialRotation;

    private Vector3 leftUpperArmInitialDirection;
    private Vector3 leftForearmInitialDirection;

    // --- Right arm ---

    private Quaternion rightUpperArmInitialRotation;
    private Quaternion rightForearmInitialRotation;

    private Vector3 rightUpperArmInitialDirection;
    private Vector3 rightForearmInitialDirection;

    // Bone lengths for 2-Bone IK
    private float leftUpperArmLength;
    private float leftForearmLength;
    private float rightUpperArmLength;
    private float rightForearmLength;
    private float avatarShoulderWidth = 0.35f;

    // --- Hands / Wrists ---

    private Quaternion leftHandInitialRotation;
    private Quaternion rightHandInitialRotation;
    private Quaternion leftHandInitialLocalRotation;
    private Quaternion rightHandInitialLocalRotation;

    // --- Head ---

    private Quaternion headInitialRotation;
    private Quaternion neckInitialRotation;

    private Vector3 initialHeadDirection;
    private Vector3 initialHeadUp;

    // --- Spine ---

    private Quaternion spineInitialRotation;

    // --- Shoulders ---

    private Vector3 leftShoulderInitialLocalPos;
    private Vector3 rightShoulderInitialLocalPos;


    // =============================================================
    // PRIVATE STATE — First-Frame Reference Values
    // =============================================================

    private Vector3 firstNose;
    private Vector3 firstLeftEar;
    private Vector3 firstRightEar;
    private bool headReferenceStored;
    private bool intentionalArmCrossingThisFrame;
    private bool handsConvergingThisFrame;
    private Quaternion leftSupportPalmHoldRotation;
    private bool leftSupportPalmHoldStored;

    [NonSerialized]
    public string activeSignId = string.Empty;

    private Quaternion leftRestPalmRotation;
    private Quaternion rightRestPalmRotation;
    private bool leftRestPalmRotationStored;
    private bool rightRestPalmRotationStored;
    private float leftHandMissingTime;
    private float rightHandMissingTime;

    // Save the arm-solver result before hand-driven forearm twist is applied.
    // Restoring these before the next solve prevents twist from accumulating.
    private Quaternion leftForearmPoseRotation;
    private Quaternion rightForearmPoseRotation;
    private bool leftForearmPoseRotationStored;
    private bool rightForearmPoseRotationStored;

    private bool spineReferenceStored;
    private Vector3 firstShoulderRight;

    private bool shoulderReferenceStored;
    private float firstLeftShoulderHipDist;
    private float firstRightShoulderHipDist;

    [Header("Return To Rest")]
    [Min(0.1f)]
    public float restPoseReturnSpeed = 6f;

    private Transform[] restPoseTransforms = Array.Empty<Transform>();
    private Quaternion[] restLocalRotations = Array.Empty<Quaternion>();
    private Vector3[] restLocalPositions = Array.Empty<Vector3>();
    private bool returningToRestPose;


    // =============================================================
    // START
    // =============================================================

    private void Start()
    {
        FindHeadBone();

        FindHipsBone();

        FindSpineBone();

        ApplyShoulderOutwardOffset();

        SaveInitialBoneState();
        CaptureRestPose();
        SaveRestPalmBases();
        if (removeBackpackGeometry)
        {
            RemoveBackpackGeometry();
        }

        if (removeRoboticLeftArmGeometry)
        {
            RemoveRoboticLeftArmGeometry();
        }

    }

    private void Update()
    {
        if (!returningToRestPose)
        {
            return;
        }

        float amount = 1f - Mathf.Exp(-restPoseReturnSpeed * Time.deltaTime);
        bool settled = true;

        for (int i = 0; i < restPoseTransforms.Length; i++)
        {
            Transform bone = restPoseTransforms[i];
            if (bone == null)
            {
                continue;
            }

            bone.localRotation = Quaternion.Slerp(
                bone.localRotation,
                restLocalRotations[i],
                amount
            );
            bone.localPosition = Vector3.Lerp(
                bone.localPosition,
                restLocalPositions[i],
                amount
            );

            if (Quaternion.Angle(bone.localRotation, restLocalRotations[i]) > 0.25f ||
                Vector3.Distance(bone.localPosition, restLocalPositions[i]) > 0.0005f)
            {
                settled = false;
            }
        }

        if (settled)
        {
            for (int i = 0; i < restPoseTransforms.Length; i++)
            {
                if (restPoseTransforms[i] == null)
                {
                    continue;
                }

                restPoseTransforms[i].localRotation = restLocalRotations[i];
                restPoseTransforms[i].localPosition = restLocalPositions[i];
            }

            returningToRestPose = false;
        }
    }

    private void CaptureRestPose()
    {
        Transform[] candidates =
        {
            leftShoulder, leftUpperArm, leftForearm, leftHand,
            rightShoulder, rightUpperArm, rightForearm, rightHand,
            headBone, neckBone, spineBone
        };

        List<Transform> bones = new List<Transform>(candidates.Length);
        foreach (Transform bone in candidates)
        {
            if (bone != null && !bones.Contains(bone))
            {
                bones.Add(bone);
            }
        }

        restPoseTransforms = bones.ToArray();
        restLocalRotations = new Quaternion[restPoseTransforms.Length];
        restLocalPositions = new Vector3[restPoseTransforms.Length];
        for (int i = 0; i < restPoseTransforms.Length; i++)
        {
            restLocalRotations[i] = restPoseTransforms[i].localRotation;
            restLocalPositions[i] = restPoseTransforms[i].localPosition;
        }
    }

    public void ReturnToRestPose()
    {
        returningToRestPose = restPoseTransforms.Length > 0;
        headReferenceStored = false;
        spineReferenceStored = false;
        shoulderReferenceStored = false;
        leftForearmPoseRotationStored = false;
        rightForearmPoseRotationStored = false;
        leftSupportPalmHoldStored = false;
        leftHandMissingTime = 0f;
        rightHandMissingTime = 0f;
    }


    // =============================================================
    // AUTO-FIND: Head Bone
    // =============================================================

    private void FindHeadBone()
    {
        Transform[] allBones =
            GetComponentsInChildren<Transform>(true);

        // Auto-find Head bone
        if (headBone == null)
        {
            foreach (Transform t in allBones)
            {
                if (t.name.Equals("head 1", StringComparison.OrdinalIgnoreCase) ||
                    t.name.Equals("head", StringComparison.OrdinalIgnoreCase))
                {
                    headBone = t;
                    Debug.Log("BodyController: Found head bone: " + t.name);
                    break;
                }
            }
        }

        // Auto-find Neck bone (node 44 in VRM humanoid)
        if (neckBone == null)
        {
            foreach (Transform t in allBones)
            {
                if (t.name.Equals("neck", StringComparison.OrdinalIgnoreCase))
                {
                    neckBone = t;
                    Debug.Log("BodyController: Found neck bone: " + t.name);
                    break;
                }
            }
        }
    }


    private void FindHipsBone()
    {
        if (hipsBone != null)
            return;

        Transform[] allBones =
            GetComponentsInChildren<Transform>(true);

        foreach (Transform t in allBones)
        {
            if (t.name.Equals("hips", StringComparison.OrdinalIgnoreCase) ||
                t.name.Equals("pelvis", StringComparison.OrdinalIgnoreCase))
            {
                hipsBone = t;
                Debug.Log("BodyController: Found hips bone: " + t.name);
                return;
            }
        }
    }


    // =============================================================
    // AUTO-FIND: Spine Bone
    // =============================================================

    private void FindSpineBone()
    {
        if (spineBone != null)
            return;

        Transform[] allBones =
            GetComponentsInChildren<Transform>(true);

        foreach (Transform t in allBones)
        {
            if (t.name.Equals("spine",
                StringComparison.OrdinalIgnoreCase))
            {
                spineBone = t;

                Debug.Log(
                    "BodyController: Found spine bone: " +
                    t.name
                );

                return;
            }
        }

        // Try partial match (e.g. "Spine1", "spine_01").
        foreach (Transform t in allBones)
        {
            if (t.name.IndexOf(
                    "spine",
                    StringComparison.OrdinalIgnoreCase
                ) >= 0)
            {
                spineBone = t;

                Debug.Log(
                    "BodyController: Found spine bone: " +
                    t.name
                );

                return;
            }
        }

        Debug.LogWarning(
            "BodyController: Could not automatically find " +
            "a spine bone. Torso rotation disabled."
        );
    }


    private Vector3 GetTorsoDepthAnchor()
    {
        if (hipsBone != null)
            return hipsBone.position;

        if (leftUpperArm != null && rightUpperArm != null)
        {
            return (leftUpperArm.position + rightUpperArm.position) * 0.5f -
                   transform.up * 0.38f;
        }

        return transform.position + transform.up * 0.9f;
    }


    private Vector3 KeepHandInFrontOfTorso(Vector3 target)
    {
        Vector3 forward = transform.forward.normalized;
        Vector3 torsoAnchor = GetTorsoDepthAnchor();
        target += forward * handForwardReachOffset;
        float depth = Vector3.Dot(target - torsoAnchor, forward);

        if (depth < minimumHandForwardClearance)
        {
            target += forward * (minimumHandForwardClearance - depth);
        }

        return target;
    }


    private Vector3 KeepLowHandOnItsSide(
        Vector3 target,
        Vector3 outwardDirection
    )
    {
        if (intentionalArmCrossingThisFrame || handsConvergingThisFrame)
            return target;

        Vector3 torsoAnchor = GetTorsoDepthAnchor();
        float height = Vector3.Dot(target - torsoAnchor, transform.up);

        // Keep this calibration local to the low, torso-level rest pose;
        // higher signing gestures remain free to cross the body midline.
        if (height > 0.62f)
            return target;

        Vector3 outward = Vector3.ProjectOnPlane(
            outwardDirection,
            transform.up
        ).normalized;

        if (outward.sqrMagnitude < 0.0001f)
            return target;

        float sideOffset = Vector3.Dot(target - torsoAnchor, outward);
        if (sideOffset < minimumLowHandSideOffset)
        {
            target += outward * (minimumLowHandSideOffset - sideOffset);
        }

        return target;
    }


    private bool HasIntentionalArmCrossing(
        Vector3 mpLeftShoulder,
        Vector3 mpRightShoulder,
        Vector3 mpLeftWrist,
        Vector3 mpRightWrist
    )
    {
        Vector3 shoulderSpan = mpLeftShoulder - mpRightShoulder;
        float shoulderWidth = shoulderSpan.magnitude;
        if (shoulderWidth < 0.001f)
            return false;

        Vector3 leftSideAxis = shoulderSpan / shoulderWidth;
        Vector3 shoulderCenter = (mpLeftShoulder + mpRightShoulder) * 0.5f;
        float leftWristSide = Vector3.Dot(mpLeftWrist - shoulderCenter, leftSideAxis);
        float rightWristSide = Vector3.Dot(mpRightWrist - shoulderCenter, leftSideAxis);
        float crossingMargin = shoulderWidth * 0.05f;

        // A wrist well past the shoulder midline is evidence of a deliberate
        // cross-body pose. Near-center noise continues to use the normal guards.
        return leftWristSide < -crossingMargin || rightWristSide > crossingMargin;
    }


    // =============================================================
    // SAVE INITIAL BONE STATE
    // =============================================================

    private void SaveInitialBoneState()
    {
        // --- Left upper arm ---

        if (leftUpperArm != null)
        {
            leftUpperArmInitialRotation =
                leftUpperArm.rotation;

            if (leftForearm != null)
            {
                leftUpperArmInitialDirection =
                    (
                        leftForearm.position -
                        leftUpperArm.position
                    ).normalized;
            }
        }

        // --- Left forearm ---

        if (leftForearm != null)
        {
            leftForearmInitialRotation =
                leftForearm.rotation;

            if (leftUpperArm != null)
            {
                leftUpperArmLength =
                    Vector3.Distance(
                        leftUpperArm.position,
                        leftForearm.position
                    );
            }

            if (leftHand != null)
            {
                leftForearmInitialDirection =
                    (
                        leftHand.position -
                        leftForearm.position
                    ).normalized;

                leftForearmLength =
                    Vector3.Distance(
                        leftForearm.position,
                        leftHand.position
                    );
            }
        }


        // --- Right upper arm ---

        if (rightUpperArm != null)
        {
            rightUpperArmInitialRotation =
                rightUpperArm.rotation;

            if (rightForearm != null)
            {
                rightUpperArmInitialDirection =
                    (
                        rightForearm.position -
                        rightUpperArm.position
                    ).normalized;
            }
        }

        // --- Right forearm ---

        if (rightForearm != null)
        {
            rightForearmInitialRotation =
                rightForearm.rotation;

            if (rightUpperArm != null)
            {
                rightUpperArmLength =
                    Vector3.Distance(
                        rightUpperArm.position,
                        rightForearm.position
                    );
            }

            if (rightHand != null)
            {
                rightForearmInitialDirection =
                    (
                        rightHand.position -
                        rightForearm.position
                    ).normalized;

                rightForearmLength =
                    Vector3.Distance(
                        rightForearm.position,
                        rightHand.position
                    );
            }
        }

        // Measure avatar shoulder width
        if (leftShoulder != null && rightShoulder != null)
        {
            avatarShoulderWidth =
                Vector3.Distance(
                    leftShoulder.position,
                    rightShoulder.position
                );
        }
        else if (leftUpperArm != null && rightUpperArm != null)
        {
            avatarShoulderWidth =
                Vector3.Distance(
                    leftUpperArm.position,
                    rightUpperArm.position
                );
        }
        if (avatarShoulderWidth < 0.05f)
        {
            avatarShoulderWidth = 0.35f;
        }


        // --- Hand bones ---

        if (leftHand != null)
        {
            leftHandInitialRotation =
                leftHand.rotation;
            leftHandInitialLocalRotation =
                leftHand.localRotation;
        }

        if (rightHand != null)
        {
            rightHandInitialRotation =
                rightHand.rotation;
            rightHandInitialLocalRotation =
                rightHand.localRotation;
        }


        // --- Head & Neck ---

        if (headBone != null)
        {
            headInitialRotation =
                headBone.rotation;

            initialHeadDirection =
                headBone.forward;

            initialHeadUp =
                headBone.up;
        }

        if (neckBone != null)
        {
            neckInitialRotation =
                neckBone.rotation;
        }


        // --- Spine ---

        if (spineBone != null)
        {
            spineInitialRotation =
                spineBone.rotation;
        }


        // --- Shoulders (local position for raise offset) ---

        if (leftShoulder != null)
        {
            leftShoulderInitialLocalPos =
                leftShoulder.localPosition;
        }

        if (rightShoulder != null)
        {
            rightShoulderInitialLocalPos =
                rightShoulder.localPosition;
        }
    }

    private void SaveRestPalmBases()
    {
        HandController handController = GetComponentInChildren<HandController>();
        if (handController == null)
        {
            handController = GetComponentInParent<HandController>();
        }

        if (handController == null)
        {
            return;
        }

        leftRestPalmRotationStored = handController.TryGetRestPalmRotation(
            true,
            leftHand,
            out leftRestPalmRotation
        );

        rightRestPalmRotationStored = handController.TryGetRestPalmRotation(
            false,
            rightHand,
            out rightRestPalmRotation
        );
    }

    private void ApplyShoulderOutwardOffset()
    {
        if (shoulderOutwardOffset <= 0f ||
            leftShoulder == null ||
            rightShoulder == null)
        {
            return;
        }

        Vector3 center =
            (leftShoulder.position + rightShoulder.position) * 0.5f;

        OffsetShoulderOutward(leftShoulder, leftShoulder.position - center);
        OffsetShoulderOutward(rightShoulder, rightShoulder.position - center);
    }

    private void OffsetShoulderOutward(Transform shoulder, Vector3 outwardWorld)
    {
        if (shoulder == null || shoulder.parent == null ||
            outwardWorld.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Vector3 outwardLocal =
            shoulder.parent.InverseTransformDirection(outwardWorld.normalized);

        shoulder.localPosition += outwardLocal * shoulderOutwardOffset;
    }


    // =============================================================
    // PUBLIC API — Called by SignPlaybackController
    // =============================================================

    /// <summary>
    /// Applies a single frame of landmark data to the avatar.
    /// Called externally by SignPlaybackController via UnityEvent.
    /// </summary>
    public void ApplyFrame(FrameData frame)
    {
        if (frame == null)
            return;

        returningToRestPose = false;

        if (frame.pose == null ||
            frame.pose.Length < 17)
            return;

        if (leftForearmPoseRotationStored && leftForearm != null)
            leftForearm.rotation = leftForearmPoseRotation;
        if (rightForearmPoseRotationStored && rightForearm != null)
            rightForearm.rotation = rightForearmPoseRotation;


        // MediaPipe Pose:
        //
        // 0  = Nose
        // 7  = Left Ear
        // 8  = Right Ear
        //
        // 11 = Left Shoulder
        // 12 = Right Shoulder
        // 13 = Left Elbow
        // 14 = Right Elbow
        // 15 = Left Wrist
        // 16 = Right Wrist
        //
        // 23 = Left Hip
        // 24 = Right Hip


        // --- Pose landmarks (normalized) ---

        bool hasWorldPose =
            frame.pose_world != null &&
            frame.pose_world.Length >= 17;

        bool hasWorldHip =
            frame.pose_world != null &&
            frame.pose_world.Length > 24;

        Vector3 nose = GetPosePosition(frame, 0, hasWorldPose);

        Vector3 leftEar = GetPosePosition(frame, 7, hasWorldPose);

        Vector3 rightEar = GetPosePosition(frame, 8, hasWorldPose);


        Vector3 leftShoulderPos = GetPosePosition(frame, 11, hasWorldPose);

        Vector3 leftElbow = GetPosePosition(frame, 13, hasWorldPose);

        Vector3 leftWristPos = GetPosePosition(frame, 15, hasWorldPose);


        Vector3 rightShoulderPos = GetPosePosition(frame, 12, hasWorldPose);

        Vector3 rightElbow = GetPosePosition(frame, 14, hasWorldPose);

        Vector3 rightWristPos = GetPosePosition(frame, 16, hasWorldPose);


        // --- Hip landmarks (for shoulders + spine) ---

        Vector3 leftHipPos = Vector3.zero;
        Vector3 rightHipPos = Vector3.zero;

        bool hasHips = frame.pose.Length > 24;

        if (hasHips)
        {
            leftHipPos = GetPosePosition(frame, 23, hasWorldPose);

            rightHipPos = GetPosePosition(frame, 24, hasWorldPose);
        }

        Vector3 mpHipCenter =
            hasHips ? (leftHipPos + rightHipPos) * 0.5f : Vector3.zero;


        // -------------------------------
        // ARM SOLVING (Directional + Spatial Metrics)
        // -------------------------------

        Vector3 leftUpperArmDir = (leftElbow - leftShoulderPos).normalized;
        Vector3 leftForearmDir  = (leftWristPos - leftElbow).normalized;

        Vector3 rightUpperArmDir = (rightElbow - rightShoulderPos).normalized;
        Vector3 rightForearmDir  = (rightWristPos - rightElbow).normalized;

        if (enableSpatialMetrics && spatialTargetWeight > 0.01f)
        {
            ApplySpatialArmIK(
                leftShoulderPos,
                rightShoulderPos,
                leftElbow,
                rightElbow,
                leftWristPos,
                rightWristPos,
                nose,
                mpHipCenter,
                hasWorldHip,
                leftUpperArmDir,
                leftForearmDir,
                rightUpperArmDir,
                rightForearmDir
            );
        }
        else
        {
            // Pure directional forward kinematics
            RotateBone(
                leftUpperArm,
                leftUpperArmInitialRotation,
                leftUpperArmInitialDirection,
                leftUpperArmDir
            );

            RotateBone(
                leftForearm,
                leftForearmInitialRotation,
                leftForearmInitialDirection,
                leftForearmDir
            );

            RotateBone(
                rightUpperArm,
                rightUpperArmInitialRotation,
                rightUpperArmInitialDirection,
                rightUpperArmDir
            );

            RotateBone(
                rightForearm,
                rightForearmInitialRotation,
                rightForearmInitialDirection,
                rightForearmDir
            );
        }

        if (leftForearm != null)
        {
            leftForearmPoseRotation = leftForearm.rotation;
            leftForearmPoseRotationStored = true;
        }
        if (rightForearm != null)
        {
            rightForearmPoseRotation = rightForearm.rotation;
            rightForearmPoseRotationStored = true;
        }


        // -------------------------------
        // WRIST ROTATION (from hand data)
        // -------------------------------

        ApplyWristRotation(
            frame.left_hand,
            leftHand,
            leftHandInitialRotation,
            leftHandInitialLocalRotation,
            true,
            ShouldHoldPassportSupportPalm(frame)
        );

        ApplyWristRotation(
            frame.right_hand,
            rightHand,
            rightHandInitialRotation,
            rightHandInitialLocalRotation,
            false,
            false
        );


        // -------------------------------
        // SHOULDER RAISE
        // -------------------------------

        if (enableShoulderRaise && hasHips)
        {
            ApplyShoulderRaise(
                leftShoulderPos,
                rightShoulderPos,
                leftHipPos,
                rightHipPos
            );
        }


        // -------------------------------
        // SPINE / TORSO ROTATION
        // -------------------------------

        if (enableSpineRotation &&
            hasHips &&
            frame.pose_world != null &&
            frame.pose_world.Length > 24)
        {
            ApplySpineRotation(frame.pose_world);
        }


        // -------------------------------
        // HEAD
        // -------------------------------

        ApplyHeadMovement(
            nose,
            leftEar,
            rightEar
        );


        // -------------------------------
        // DEBUG LINES
        // -------------------------------

        if (showDebugLines)
        {
            Debug.DrawLine(
                leftShoulderPos,
                leftElbow,
                Color.green
            );

            Debug.DrawLine(
                leftElbow,
                leftWristPos,
                Color.green
            );

            Debug.DrawLine(
                rightShoulderPos,
                rightElbow,
                Color.blue
            );

            Debug.DrawLine(
                rightElbow,
                rightWristPos,
                Color.blue
            );

            Debug.DrawLine(
                leftEar,
                rightEar,
                Color.yellow
            );
        }
    }


    /// <summary>
    /// Resets first-frame reference data so the next
    /// ApplyFrame call re-captures the neutral pose.
    /// </summary>
    public void ResetReferences()
    {
        headReferenceStored = false;
        leftHandMissingTime = 0f;
        rightHandMissingTime = 0f;
        spineReferenceStored = false;
        shoulderReferenceStored = false;

        // Restore shoulder positions to their rest pose.
        if (leftShoulder != null)
        {
            leftShoulder.localPosition =
                leftShoulderInitialLocalPos;
        }

        if (rightShoulder != null)
        {
            rightShoulder.localPosition =
                rightShoulderInitialLocalPos;
        }
    }

    private Vector3 GetPosePosition(
        FrameData frame,
        int index,
        bool hasWorldPose
    )
    {
        if (hasWorldPose &&
            frame.pose_world.Length > index &&
            frame.pose_world[index] != null)
        {
            return LandmarkUtils.WorldToUnity(
                frame.pose_world[index],
                movementScale,
                mirrorX
            );
        }

        return LandmarkUtils.MediaPipeToUnity(
            frame.pose[index],
            movementScale,
            mirrorX,
            landmarkHorizontalScale
        );
    }


    // =============================================================
    // HEAD MOVEMENT
    // =============================================================

    private void ApplyHeadMovement(
        Vector3 nose,
        Vector3 leftEar,
        Vector3 rightEar
    )
    {
        if (headBone == null)
            return;


        // Store the first frame as the neutral head pose.
        if (!headReferenceStored)
        {
            firstNose = nose;
            firstLeftEar = leftEar;
            firstRightEar = rightEar;

            headReferenceStored = true;

            return;
        }


        // ----------------------------------
        // Ear-to-ear vector
        // ----------------------------------

        Vector3 currentEarVector =
            (
                rightEar -
                leftEar
            ).normalized;

        Vector3 initialEarVector =
            (
                firstRightEar -
                firstLeftEar
            ).normalized;


        if (currentEarVector.sqrMagnitude <
            0.000001f)
            return;

        if (initialEarVector.sqrMagnitude <
            0.000001f)
            return;


        // ----------------------------------
        // Detect side-to-side head rotation
        // ----------------------------------

        Quaternion horizontalRotation =
            Quaternion.FromToRotation(
                initialEarVector,
                currentEarVector
            );


        // ----------------------------------
        // Nose vertical movement
        // gives a small nod/tilt component
        // ----------------------------------

        float verticalDifference =
            nose.y -
            firstNose.y;


        float verticalAngle =
            Mathf.Clamp(
                -verticalDifference * 100f,
                -maxHeadRotation,
                maxHeadRotation
            );


        Quaternion verticalRotation =
            Quaternion.AngleAxis(
                verticalAngle,
                headBone.right
            );


        // ----------------------------------
        // Head tilt (Z-axis roll)
        //
        // Measure the ear-to-ear vector's
        // angle against horizontal to get
        // head tilt rotation.
        // ----------------------------------

        float earDeltaY =
            currentEarVector.y -
            initialEarVector.y;

        float tiltAngle =
            Mathf.Clamp(
                earDeltaY * 90f,
                -maxHeadRotation,
                maxHeadRotation
            );

        Quaternion tiltRotation =
            Quaternion.AngleAxis(
                tiltAngle,
                headBone.forward
            );


        // ----------------------------------
        // Combine and smooth
        // Apply relative rotation deltas in local space
        // ----------------------------------

        Quaternion combinedDelta =
            horizontalRotation *
            verticalRotation *
            tiltRotation;

        // Distribute rotation: 30% neck, 70% head to eliminate stiff neck
        Quaternion neckDelta = Quaternion.Slerp(Quaternion.identity, combinedDelta, 0.30f);
        Quaternion headDelta = Quaternion.Slerp(Quaternion.identity, combinedDelta, 0.70f);

        Quaternion desiredHeadRot = headInitialRotation * headDelta;
        Quaternion desiredNeckRot = (neckBone != null) ? neckInitialRotation * neckDelta : Quaternion.identity;

        float headInterpolation =
            1f - Mathf.Exp(-rotationSmooth * headMovementAmount * Time.deltaTime);

        headBone.rotation =
            Quaternion.Slerp(headBone.rotation, desiredHeadRot, headInterpolation);

        if (neckBone != null)
        {
            neckBone.rotation =
                Quaternion.Slerp(neckBone.rotation, desiredNeckRot, headInterpolation);
        }
    }


    // =============================================================
    // WRIST ROTATION
    //
    // Uses hand landmarks 0 (wrist), 5 (index MCP),
    // and 17 (pinky MCP) to compute the palm plane
    // orientation and apply it to the hand bone.
    // =============================================================

    private void ApplyWristRotation(
        Landmark[] handLandmarks,
        Transform handBone,
        Quaternion handInitialRotation,
        Quaternion handInitialLocalRotation,
        bool isLeft,
        bool stabilizeSupportPalm
    )
    {
        if (handBone == null)
            return;

        if (handLandmarks == null ||
            handLandmarks.Length < 18)
        {
            if (isLeft)
            {
                leftHandMissingTime += Time.deltaTime;
                if (leftHandMissingTime < wristLossGracePeriod)
                {
                    return;
                }
            }
            else
            {
                rightHandMissingTime += Time.deltaTime;
                if (rightHandMissingTime < wristLossGracePeriod)
                {
                    return;
                }
            }

            float resetInterpolation =
                1f - Mathf.Exp(-rotationSmooth * Time.deltaTime);

            handBone.localRotation = Quaternion.Slerp(
                handBone.localRotation,
                handInitialLocalRotation,
                resetInterpolation
            );
            return;
        }

        if (isLeft)
        {
            leftHandMissingTime = 0f;
        }
        else
        {
            rightHandMissingTime = 0f;
        }


        // --- Key landmarks ---

        Vector3 wrist =
            LandmarkUtils.HandToUnity(
                handLandmarks[0],
                landmarkHorizontalScale,
                mirrorX
            );

        Vector3 indexMCP =
            LandmarkUtils.HandToUnity(
                handLandmarks[5],
                landmarkHorizontalScale,
                mirrorX
            );

        Vector3 pinkyMCP =
            LandmarkUtils.HandToUnity(
                handLandmarks[17],
                landmarkHorizontalScale,
                mirrorX
            );


        // --- Palm plane vectors ---

        Vector3 palmForward =
            (indexMCP - wrist).normalized;

        Vector3 palmRight =
            (pinkyMCP - wrist).normalized;

        Vector3 palmNormal =
            Vector3.Cross(
                palmForward,
                palmRight
            ).normalized;

        if (palmForward.sqrMagnitude < 0.000001f)
            return;

        if (palmNormal.sqrMagnitude < 0.000001f)
            return;


        Quaternion wristRotation =
            Quaternion.LookRotation(
                palmForward,
                palmNormal
            );


        // Match the measured palm orientation against the avatar's
        // own rest-pose palm basis. This also works when the video
        // does not detect a neutral hand pose at its first frame.
        bool hasRestPalmBasis = isLeft
            ? leftRestPalmRotationStored
            : rightRestPalmRotationStored;

        Quaternion restPalmRotation = isLeft
            ? leftRestPalmRotation
            : rightRestPalmRotation;

        Quaternion targetRotation = hasRestPalmBasis
            ? wristRotation * Quaternion.Inverse(restPalmRotation) * handInitialRotation
            : handInitialRotation;

        if (isLeft && stabilizeSupportPalm && leftSupportPalmHoldStored)
        {
            targetRotation = leftSupportPalmHoldRotation;
        }

        Transform forearmBone = isLeft ? leftForearm : rightForearm;
        if (forearmBone != null && forearmTwistStrength > 0f)
        {
            Vector3 forearmAxis = (handBone.position - forearmBone.position).normalized;
            Quaternion handDelta = targetRotation * Quaternion.Inverse(handInitialRotation);
            Quaternion forearmTwist = ExtractTwistAroundAxis(handDelta, forearmAxis);

            if (forearmAxis.sqrMagnitude > 0.000001f)
            {
                Quaternion targetForearmRotation = forearmTwist * forearmBone.rotation;
                float twistInterpolation =
                    1f - Mathf.Exp(
                        GetWristRotationSmooth(stabilizeSupportPalm) *
                        wristRotationStrength *
                        Time.deltaTime
                    );

                forearmBone.rotation = Quaternion.Slerp(
                    forearmBone.rotation,
                    targetForearmRotation,
                    twistInterpolation * forearmTwistStrength
                );
            }
        }


        // --- Blend by strength and smooth ---

        float interpolation =
            1f -
            Mathf.Exp(
                -GetWristRotationSmooth(stabilizeSupportPalm) *
                wristRotationStrength *
                Time.deltaTime
            );

        handBone.rotation =
            Quaternion.Slerp(
                handBone.rotation,
                targetRotation,
                interpolation
            );
    }

    private float GetWristRotationSmooth(bool stabilizeSupportPalm)
    {
        // A supporting palm should not chase small landmark orientation noise
        // while the other hand is touching it. Keep the source orientation,
        // but ease it more gently; the stamping hand remains fully responsive.
        return stabilizeSupportPalm
            ? Mathf.Min(rotationSmooth, 3f)
            : rotationSmooth;
    }

    private bool ShouldHoldPassportSupportPalm(FrameData frame)
    {
        bool hold = string.Equals(activeSignId, "passport", StringComparison.OrdinalIgnoreCase) &&
                    frame != null && frame.frame >= 38 && frame.frame <= 58;

        if (hold && !leftSupportPalmHoldStored && leftHand != null)
        {
            // Capture the left palm after it has turned into the book-support
            // pose, then keep that orientation while the right hand stamps.
            leftSupportPalmHoldRotation = leftHand.rotation;
            leftSupportPalmHoldStored = true;
        }
        else if (!hold)
        {
            leftSupportPalmHoldStored = false;
        }

        return hold;
    }

    private static Quaternion ExtractTwistAroundAxis(Quaternion rotation, Vector3 axis)
    {
        if (axis.sqrMagnitude < 0.000001f)
            return Quaternion.identity;

        axis.Normalize();
        Vector3 vectorPart = new Vector3(rotation.x, rotation.y, rotation.z);
        Vector3 projected = Vector3.Project(vectorPart, axis);
        Quaternion twist = new Quaternion(projected.x, projected.y, projected.z, rotation.w);

        float magnitude = Mathf.Sqrt(
            twist.x * twist.x + twist.y * twist.y + twist.z * twist.z + twist.w * twist.w
        );
        if (magnitude < 0.000001f)
            return Quaternion.identity;

        float inverseMagnitude = 1f / magnitude;
        return new Quaternion(
            twist.x * inverseMagnitude,
            twist.y * inverseMagnitude,
            twist.z * inverseMagnitude,
            twist.w * inverseMagnitude
        );
    }

    private void RemoveBackpackGeometry()
    {
        SkinnedMeshRenderer[] renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (SkinnedMeshRenderer meshRenderer in renderers)
        {
            Mesh sourceMesh = meshRenderer.sharedMesh;
            if (sourceMesh == null)
                continue;

            // Avoid cloning an already-filtered mesh if cleanup is invoked again.
            if (sourceMesh.name.IndexOf("(Backpack Removed)", StringComparison.OrdinalIgnoreCase) >= 0)
                continue;

            Material[] materials = meshRenderer.sharedMaterials;
            int[] backpackSubmeshes = new int[materials.Length];
            int backpackSubmeshCount = 0;

            for (int i = 0; i < materials.Length && i < sourceMesh.subMeshCount; i++)
            {
                Material material = materials[i];
                if (material != null &&
                    material.name.IndexOf("backpack", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    backpackSubmeshes[backpackSubmeshCount++] = i;
                }
            }

            if (backpackSubmeshCount == 0)
                continue;

            // Clone the imported mesh before hiding its backpack-only material
            // slots, so the source VRM and any other instances stay unchanged.
            Mesh avatarMesh = Instantiate(sourceMesh);
            avatarMesh.name = sourceMesh.name + " (Backpack Removed)";
            for (int i = 0; i < backpackSubmeshCount; i++)
            {
                avatarMesh.SetIndices(
                    Array.Empty<int>(),
                    MeshTopology.Triangles,
                    backpackSubmeshes[i],
                    false
                );
            }

            meshRenderer.sharedMesh = avatarMesh;
        }
    }


    private void RemoveRoboticLeftArmGeometry()
    {
        // Keep the normal body/arm mesh intact. Only Seed-san's distinct
        // robotic overlay renderer is disabled, and only at runtime.
        SkinnedMeshRenderer[] renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (SkinnedMeshRenderer meshRenderer in renderers)
        {
            if (string.Equals(meshRenderer.gameObject.name, "robo_arm", StringComparison.OrdinalIgnoreCase) ||
                (meshRenderer.sharedMesh != null &&
                 string.Equals(meshRenderer.sharedMesh.name, "robo_arm", StringComparison.OrdinalIgnoreCase)))
            {
                meshRenderer.enabled = false;
            }
        }
    }


    // =============================================================
    // SHOULDER RAISE
    //
    // Compares the vertical distance between shoulder and
    // hip to the first-frame reference. A decrease in
    // distance means the shoulder is being raised (shrug).
    // =============================================================

    private void ApplyShoulderRaise(
        Vector3 leftShoulderPos,
        Vector3 rightShoulderPos,
        Vector3 leftHipPos,
        Vector3 rightHipPos
    )
    {
        // --- Store first-frame distances ---

        if (!shoulderReferenceStored)
        {
            firstLeftShoulderHipDist =
                leftShoulderPos.y - leftHipPos.y;

            firstRightShoulderHipDist =
                rightShoulderPos.y - rightHipPos.y;

            shoulderReferenceStored = true;

            return;
        }


        // --- Left shoulder ---

        if (leftShoulder != null)
        {
            float currentLeftDist =
                leftShoulderPos.y - leftHipPos.y;

            float leftDelta =
                currentLeftDist - firstLeftShoulderHipDist;

            float leftOffset =
                leftDelta * shoulderRaiseScale;

            Vector3 targetLeftPos =
                leftShoulderInitialLocalPos +
                new Vector3(0f, leftOffset, 0f);

            leftShoulder.localPosition =
                Vector3.Lerp(
                    leftShoulder.localPosition,
                    targetLeftPos,
                    1f -
                    Mathf.Exp(
                        -rotationSmooth *
                        Time.deltaTime
                    )
                );
        }


        // --- Right shoulder ---

        if (rightShoulder != null)
        {
            float currentRightDist =
                rightShoulderPos.y - rightHipPos.y;

            float rightDelta =
                currentRightDist - firstRightShoulderHipDist;

            float rightOffset =
                rightDelta * shoulderRaiseScale;

            Vector3 targetRightPos =
                rightShoulderInitialLocalPos +
                new Vector3(0f, rightOffset, 0f);

            rightShoulder.localPosition =
                Vector3.Lerp(
                    rightShoulder.localPosition,
                    targetRightPos,
                    1f -
                    Mathf.Exp(
                        -rotationSmooth *
                        Time.deltaTime
                    )
                );
        }
    }


    // =============================================================
    // SPINE / TORSO ROTATION
    //
    // Uses pose_world landmarks (shoulders 11/12 and hips
    // 23/24) to compute torso orientation relative to the
    // avatar's upright pose.
    //
    //  - Shoulder midpoint → hip midpoint = spine direction
    //  - Shoulder-to-shoulder vs hip-to-hip = torso twist
    // =============================================================

    private void ApplySpineRotation(
        Landmark[] poseWorld
    )
    {
        if (spineBone == null)
            return;

        if (poseWorld == null ||
            poseWorld.Length < 25)
            return;


        Vector3 wLeftShoulder =
            LandmarkUtils.WorldToUnity(
                poseWorld[11],
                1f,
                mirrorX
            );

        Vector3 wRightShoulder =
            LandmarkUtils.WorldToUnity(
                poseWorld[12],
                1f,
                mirrorX
            );

        Vector3 wLeftHip =
            LandmarkUtils.WorldToUnity(
                poseWorld[23],
                1f,
                mirrorX
            );

        Vector3 wRightHip =
            LandmarkUtils.WorldToUnity(
                poseWorld[24],
                1f,
                mirrorX
            );


        // --- Midpoints ---

        Vector3 shoulderMid =
            (wLeftShoulder + wRightShoulder) * 0.5f;

        Vector3 hipMid =
            (wLeftHip + wRightHip) * 0.5f;


        // --- Spine direction (hip → shoulder) ---

        Vector3 spineDirection =
            (shoulderMid - hipMid).normalized;

        // --- Shoulder-right vector ---

        Vector3 shoulderRight =
            (wRightShoulder - wLeftShoulder).normalized;


        if (spineDirection.sqrMagnitude < 0.000001f)
            return;

        if (shoulderRight.sqrMagnitude < 0.000001f)
            return;


        // --- Store first-frame reference ---

        if (!spineReferenceStored)
        {
            firstShoulderRight = shoulderRight;

            spineReferenceStored = true;
        }


        // Derive lean from the avatar's upright axis instead of treating
        // the video's first frame as neutral. The first captured frame may
        // already be leaning forward, which otherwise makes later frames
        // appear to bend backward.
        Quaternion absoluteTilt =
            Quaternion.FromToRotation(transform.up, spineDirection);

        Quaternion spineTilt =
            Quaternion.Slerp(
                Quaternion.identity,
                absoluteTilt,
                Mathf.Clamp01(spineRotationStrength)
            );

        // Torso twist around the spine axis.
        // Project shoulder vectors onto the plane
        // perpendicular to the spine, then measure twist.

        Vector3 firstRightProjected =
            Vector3.ProjectOnPlane(
                firstShoulderRight,
                spineDirection
            ).normalized;

        Vector3 currentRightProjected =
            Vector3.ProjectOnPlane(
                shoulderRight,
                spineDirection
            ).normalized;

        Quaternion spineTwist =
            Quaternion.identity;

        if (firstRightProjected.sqrMagnitude > 0.000001f &&
            currentRightProjected.sqrMagnitude > 0.000001f)
        {
            spineTwist =
                Quaternion.FromToRotation(
                    firstRightProjected,
                    currentRightProjected
                );
        }

        spineTwist = Quaternion.Slerp(
            Quaternion.identity,
            spineTwist,
            Mathf.Clamp01(spineRotationStrength)
        );


        Quaternion combinedDelta =
            spineTwist * spineTilt;

        Quaternion targetRotation =
            combinedDelta *
            spineInitialRotation;


        // --- Blend by strength and smooth ---

        float interpolation =
            1f -
            Mathf.Exp(
                -rotationSmooth *
                Time.deltaTime
            );

        spineBone.rotation =
            Quaternion.Slerp(
                spineBone.rotation,
                targetRotation,
                interpolation
            );
    }


    // =============================================================
    // BONE ROTATION HELPER
    //
    // Rotates a bone from its initial rotation by the delta
    // between an initial direction and a target direction.
    // Uses exponential smoothing.
    // =============================================================

    private void RotateBone(
        Transform bone,
        Quaternion initialRotation,
        Vector3 initialDirection,
        Vector3 targetDirection
    )
    {
        if (bone == null)
            return;

        if (initialDirection.sqrMagnitude <
            0.000001f)
            return;

        if (targetDirection.sqrMagnitude <
            0.000001f)
            return;


        initialDirection.Normalize();
        targetDirection.Normalize();


        Quaternion delta =
            Quaternion.FromToRotation(
                initialDirection,
                targetDirection
            );


        Quaternion targetRotation =
            delta *
            initialRotation;


        float interpolation =
            1f -
            Mathf.Exp(
                -rotationSmooth *
                Time.deltaTime
            );


        bone.rotation =
            Quaternion.Slerp(
                bone.rotation,
                targetRotation,
                interpolation
            );
    }


    // =============================================================
    // SPATIAL METRICS & TWO-BONE IK SOLVER
    //
    // Solves arm positioning by calculating spatial relationships
    // (Hand-to-Head, Hand-to-Hand, Hand-to-Chest) normalized by
    // shoulder width, then driving upper arm and forearm rotations.
    // =============================================================

    private void ApplySpatialArmIK(
        Vector3 mpLeftShoulder,
        Vector3 mpRightShoulder,
        Vector3 mpLeftElbow,
        Vector3 mpRightElbow,
        Vector3 mpLeftWrist,
        Vector3 mpRightWrist,
        Vector3 mpNose,
        Vector3 mpHipCenter,
        bool hasWorldHip,
        Vector3 leftUpperArmDir,
        Vector3 leftForearmDir,
        Vector3 rightUpperArmDir,
        Vector3 rightForearmDir
    )
    {
        // 1. MediaPipe scale factor (shoulder width)
        float mpShoulderDist =
            Vector3.Distance(mpLeftShoulder, mpRightShoulder);

        if (mpShoulderDist < 0.05f)
            mpShoulderDist = 0.35f;

        float metricScale =
            (avatarShoulderWidth / mpShoulderDist) * spatialReachScale;

        // 2. Head anchor position in Unity world
        Vector3 avatarHeadPos =
            (headBone != null)
                ? headBone.position
                : ((leftUpperArm != null && rightUpperArm != null)
                    ? (leftUpperArm.position + rightUpperArm.position) * 0.5f + Vector3.up * 0.3f
                    : transform.position + Vector3.up * 1.5f);

        // 3. Normalized spatial offsets from head / face anchor
        // Attenuate depth (Z) to avoid overextending forward, and add vertical boost when hand is raised
        Vector3 rawLeftOffset = (mpLeftWrist - mpNose) * metricScale;
        Vector3 rawRightOffset = (mpRightWrist - mpNose) * metricScale;

        // Apply depth damping (Z-axis in Unity space)
        rawLeftOffset.z *= spatialDepthScale;
        rawRightOffset.z *= spatialDepthScale;

        // Ease the wrist toward the face as it rises. Pose wrist landmarks
        // often sit below the fingertips in sign clips, so a small height
        // correction helps a hand reach the chin without lifting resting arms.
        if (rawLeftOffset.y > -0.45f)
        {
            float boostFactor = Mathf.InverseLerp(-0.45f, 0.1f, rawLeftOffset.y);
            rawLeftOffset.y += handHeightBoost * boostFactor;
        }

        if (rawRightOffset.y > -0.45f)
        {
            float boostFactor = Mathf.InverseLerp(-0.45f, 0.1f, rawRightOffset.y);
            rawRightOffset.y +=
                (handHeightBoost + rightHandFaceHeightBoost) * boostFactor;
        }

        Vector3 targetLeftHandPos = avatarHeadPos + rawLeftOffset;
        Vector3 targetRightHandPos = avatarHeadPos + rawRightOffset;

        // pose_world positions are relative to the hip center. Anchor them
        // to the avatar's hips so stomach-level hands keep their true depth
        // instead of inheriting the avatar head's forward offset.
        if (hasWorldHip && hipsBone != null)
        {
            Vector3 leftHipRelativeWrist = mpLeftWrist - mpHipCenter;
            Vector3 rightHipRelativeWrist = mpRightWrist - mpHipCenter;
            leftHipRelativeWrist.z *= spatialDepthScale;
            rightHipRelativeWrist.z *= spatialDepthScale;

            targetLeftHandPos =
                hipsBone.position + leftHipRelativeWrist * metricScale;

            targetRightHandPos =
                hipsBone.position + rightHipRelativeWrist * metricScale;

            // Preserve the face-reach correction while using hip-relative
            // coordinates for the actual target position.
            float leftHeightCorrection =
                rawLeftOffset.y - (mpLeftWrist.y - mpNose.y) * metricScale;

            float rightHeightCorrection =
                rawRightOffset.y - (mpRightWrist.y - mpNose.y) * metricScale;

            targetLeftHandPos += transform.up * leftHeightCorrection;
            targetRightHandPos += transform.up * rightHeightCorrection;
        }

        Vector3 avatarShoulderCenter =
            (leftShoulder != null && rightShoulder != null)
                ? (leftShoulder.position + rightShoulder.position) * 0.5f
                : transform.position;

        Vector3 leftElbowOutward =
            (leftShoulder != null)
                ? leftShoulder.position - avatarShoulderCenter
                : -transform.right;

        Vector3 rightElbowOutward =
            (rightShoulder != null)
                ? rightShoulder.position - avatarShoulderCenter
                : transform.right;

        // 4. Enforce minimum hand separation to prevent mesh collision / clipping
        intentionalArmCrossingThisFrame = HasIntentionalArmCrossing(
            mpLeftShoulder,
            mpRightShoulder,
            mpLeftWrist,
            mpRightWrist
        );

        // Treat close wrist targets as an intentional two-hand meeting pose.
        // This avoids forcing each low hand back to its own side just as the
        // hands approach the center of the body.
        handsConvergingThisFrame =
            Vector3.Distance(mpLeftWrist, mpRightWrist) < mpShoulderDist * 0.55f;

        if (handsConvergingThisFrame)
        {
            // Normalize the hand gap toward the body center when the source
            // clip brings both hands together. Wrist landmarks alone do not
            // account for the avatar's palm width, so a modest compression
            // helps fingertip-contact signs meet instead of stopping at a gap.
            Vector3 center = (targetLeftHandPos + targetRightHandPos) * 0.5f;
            Vector3 lateral = Vector3.ProjectOnPlane(
                targetLeftHandPos - targetRightHandPos,
                transform.up
            );
            Vector3 depthAndHeight = Vector3.ProjectOnPlane(
                targetLeftHandPos - targetRightHandPos,
                lateral.sqrMagnitude > 0.0001f ? lateral.normalized : transform.right
            );
            targetLeftHandPos = center + depthAndHeight * 0.5f + lateral * 0.36f;
            targetRightHandPos = center - depthAndHeight * 0.5f - lateral * 0.36f;
        }

        if (enforceHandSeparation && !intentionalArmCrossingThisFrame)
        {
            float requiredHandDistance = handsConvergingThisFrame
                ? minContactHandDistance
                : minHandDistance;
            float handDist =
                Vector3.Distance(targetLeftHandPos, targetRightHandPos);

            if (handDist < requiredHandDistance)
            {
                Vector3 center =
                    (targetLeftHandPos + targetRightHandPos) * 0.5f;

                Vector3 pushDir = targetLeftHandPos - targetRightHandPos;
                if (pushDir.sqrMagnitude < 0.0001f)
                {
                    // Coincident targets have no usable separation direction.
                    // Use the avatar's shoulder axis to keep both hands clear
                    // of each other without pushing either hand into the torso.
                    pushDir = leftShoulder != null && rightShoulder != null
                        ? leftShoulder.position - rightShoulder.position
                        : -transform.right;
                }

                pushDir = Vector3.ProjectOnPlane(pushDir, transform.up).normalized;
                if (pushDir.sqrMagnitude < 0.0001f)
                    pushDir = -transform.right;

                targetLeftHandPos  = center + pushDir * (requiredHandDistance * 0.5f);
                targetRightHandPos = center - pushDir * (requiredHandDistance * 0.5f);
            }
        }

        // Hand-separation can shift targets slightly along the forward axis,
        // so apply the torso clearance after that adjustment as well.
        targetLeftHandPos = KeepLowHandOnItsSide(
            targetLeftHandPos,
            leftElbowOutward
        );

        targetRightHandPos = KeepLowHandOnItsSide(
            targetRightHandPos,
            rightElbowOutward
        );

        targetLeftHandPos = KeepHandInFrontOfTorso(targetLeftHandPos);
        targetRightHandPos = KeepHandInFrontOfTorso(targetRightHandPos);

        // 5. Solve Left Arm (Analytical 2-Bone IK)
        if (leftUpperArm != null && leftForearm != null)
        {
            SolveArmWithBlend(
                leftUpperArm,
                leftForearm,
                leftUpperArmInitialRotation,
                leftForearmInitialRotation,
                leftUpperArmInitialDirection,
                leftForearmInitialDirection,
                leftUpperArmLength,
                leftForearmLength,
                targetLeftHandPos,
                leftUpperArmDir,
                leftForearmDir,
                mpLeftElbow - mpLeftShoulder,
                leftElbowOutward
            );
        }

        // 6. Solve Right Arm (Analytical 2-Bone IK)
        if (rightUpperArm != null && rightForearm != null)
        {
            SolveArmWithBlend(
                rightUpperArm,
                rightForearm,
                rightUpperArmInitialRotation,
                rightForearmInitialRotation,
                rightUpperArmInitialDirection,
                rightForearmInitialDirection,
                rightUpperArmLength,
                rightForearmLength,
                targetRightHandPos,
                rightUpperArmDir,
                rightForearmDir,
                mpRightElbow - mpRightShoulder,
                rightElbowOutward
            );
        }
    }


    // =============================================================
    // SOLVE ARM WITH BLEND (FK Angle + Spatial IK Target)
    // =============================================================

    private void SolveArmWithBlend(
        Transform upperArm,
        Transform forearm,
        Quaternion upperArmInitRot,
        Quaternion forearmInitRot,
        Vector3 upperArmInitDir,
        Vector3 forearmInitDir,
        float upperArmLen,
        float forearmLen,
        Vector3 spatialTargetPos,
        Vector3 fkUpperArmDir,
        Vector3 fkForearmDir,
        Vector3 preferredElbowDirection,
        Vector3 elbowOutwardDirection
    )
    {
        // Pure FK target rotations
        Quaternion fkUpperRot =
            Quaternion.FromToRotation(upperArmInitDir, fkUpperArmDir) * upperArmInitRot;

        Quaternion fkForearmRot =
            Quaternion.FromToRotation(forearmInitDir, fkForearmDir) * forearmInitRot;

        // IK target rotations
        Quaternion ikUpperRot = fkUpperRot;
        Quaternion ikForearmRot = fkForearmRot;

        SolveTwoBoneIK(
            upperArm.position,
            spatialTargetPos,
            upperArmLen > 0.01f ? upperArmLen : 0.22f,
            forearmLen > 0.01f ? forearmLen : 0.20f,
            upperArmInitDir,
            forearmInitDir,
            upperArmInitRot,
            forearmInitRot,
            preferredElbowDirection,
            elbowOutwardDirection,
            out ikUpperRot,
            out ikForearmRot
        );

        // Blend between FK and IK based on spatialTargetWeight
        Quaternion targetUpper =
            Quaternion.Slerp(fkUpperRot, ikUpperRot, spatialTargetWeight);

        Quaternion targetForearm =
            Quaternion.Slerp(fkForearmRot, ikForearmRot, spatialTargetWeight);

        float interpolation =
            1f - Mathf.Exp(-rotationSmooth * Time.deltaTime);

        upperArm.rotation =
            Quaternion.Slerp(upperArm.rotation, targetUpper, interpolation);

        forearm.rotation =
            Quaternion.Slerp(forearm.rotation, targetForearm, interpolation);
    }


    // =============================================================
    // ANALYTICAL TWO-BONE IK SOLVER
    //
    // Mathematically calculates shoulder and elbow rotations
    // without stretching bones or creating numerical instability.
    // =============================================================

    private void SolveTwoBoneIK(
        Vector3 rootPos,
        Vector3 targetPos,
        float lengthA,
        float lengthB,
        Vector3 initDirA,
        Vector3 initDirB,
        Quaternion initRotA,
        Quaternion initRotB,
        Vector3 preferredElbowDirection,
        Vector3 elbowOutwardDirection,
        out Quaternion rotA,
        out Quaternion rotB
    )
    {
        Vector3 toTarget = targetPos - rootPos;
        float targetDist = toTarget.magnitude;

        if (targetDist < 0.0001f)
        {
            toTarget = initDirA.normalized;
            targetDist = 0.0001f;
        }

        Vector3 targetDirection = toTarget.normalized;

        // Clamp distance to bone reach limits (99% max stretch)
        float maxReach = (lengthA + lengthB) * 0.999f;
        float minReach = Mathf.Abs(lengthA - lengthB) * 1.05f;
        targetDist = Mathf.Clamp(targetDist, minReach, maxReach);

        toTarget = targetDirection * targetDist;
        Vector3 reachableTargetPos = rootPos + toTarget;

        // Law of Cosines to solve joint angles
        // angleA = angle at root (between shoulder->target and shoulder->elbow)
        float cosA =
            (lengthA * lengthA + targetDist * targetDist - lengthB * lengthB) /
            (2f * lengthA * targetDist);

        cosA = Mathf.Clamp(cosA, -1f, 1f);
        // angleB = exterior angle at elbow joint
        float cosB =
            (lengthA * lengthA + lengthB * lengthB - targetDist * targetDist) /
            (2f * lengthA * lengthB);

        cosB = Mathf.Clamp(cosB, -1f, 1f);
        float angleB = Mathf.Acos(cosB) * Mathf.Rad2Deg;

        // Keep the elbow on the same side of the shoulder-to-wrist line
        // as the tracked elbow. A fixed world-space bend hint made elbows
        // fold inward or behind the torso when the signer changed direction.
        Vector3 bendDirection = Vector3.ProjectOnPlane(
            preferredElbowDirection,
            targetDirection
        );

        if (bendDirection.sqrMagnitude < 0.0001f)
        {
            bendDirection = Vector3.ProjectOnPlane(Vector3.up, targetDirection);
        }

        if (bendDirection.sqrMagnitude < 0.0001f)
        {
            bendDirection = Vector3.ProjectOnPlane(Vector3.right, targetDirection);
        }

        bendDirection.Normalize();

        // Keep the projected elbow direction on the outside of the torso.
        // The captured elbow still determines the bend plane; this only
        // adds enough lateral bias to prevent the upper arm from cutting
        // through the chest when the tracked elbow is noisy or occluded.
        Vector3 outwardInBendPlane = Vector3.ProjectOnPlane(
            elbowOutwardDirection,
            targetDirection
        );

        if (outwardInBendPlane.sqrMagnitude > 0.0001f)
        {
            outwardInBendPlane.Normalize();

            float outwardDot = Vector3.Dot(bendDirection, outwardInBendPlane);
            if (outwardDot < elbowOutwardBias)
            {
                float outwardBlend =
                    (elbowOutwardBias - outwardDot) /
                    Mathf.Max(0.0001f, 1f - outwardDot);

                bendDirection = Vector3.Slerp(
                    bendDirection,
                    outwardInBendPlane,
                    Mathf.Clamp01(outwardBlend)
                ).normalized;
            }
        }

        float sinA = Mathf.Sqrt(Mathf.Max(0f, 1f - cosA * cosA));
        Vector3 elbowDir =
            (targetDirection * cosA + bendDirection * sinA).normalized;

        Vector3 elbowPos = rootPos + elbowDir * lengthA;

        // Forearm direction
        Vector3 forearmDir = (reachableTargetPos - elbowPos).normalized;

        // Compute output rotations
        rotA =
            Quaternion.FromToRotation(initDirA, elbowDir) * initRotA;

        rotB =
            Quaternion.FromToRotation(initDirB, forearmDir) * initRotB;
    }

}
