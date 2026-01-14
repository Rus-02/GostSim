using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

[CreateAssetMenu(fileName = "FixtureAnimationData", menuName = "Data Models/Fixture Animation Data")]
public class FixtureAnimationData : ScriptableObject
{
    public AnimationDirection animationDirection;

    [System.Serializable]
    public class AnimationStep
    {
        public AnimationStepType stepType;

        [Header("Move Step Parameters")]
        public Vector3 moveDirection;
        public float moveDuration = 1f;
        public Ease moveEase = Ease.Linear;

        [Header("Rotate Step Parameters")]
        public Vector3 rotationAngle;
        public float rotationDuration = 1f;
        public Ease rotationEase = Ease.Linear;

        [Header("Wait Step Parameters")]
        public float waitTime = 1f;
    }

    public List<AnimationStep> animationSteps = new List<AnimationStep>();
}