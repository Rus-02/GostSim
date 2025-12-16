using UnityEngine;
using System.Collections.Generic;

public class HydraulicMachineConfig : MachineConfigBase
{
    [Header("General Machine Settings")]
    [Tooltip("Главная ось движения механизмов (Траверсы и Рамы) в ЛОКАЛЬНЫХ координатах.\n(0,1,0) - Стандарт Unity (Y)\n(0,0,1) - Импорт из Blender (Z)\n(1,0,0) - Горизонтальная машина")]
    public Vector3 LocalMotionAxis = Vector3.up; 
    
    // --- Traverse Config ---
    [Header("Traverse Config")]
    public GameObject MovingTraverseRoot;
    public List<GameObject> RotatingParts;
    public GameObject BeltGameObject;
    public GRMTextureMovement BeltTextureMovement;
    public List<Transform> MovingTraverseBellowBones;

    [Header("Traverse Speeds")]
    public float TraverseFastSpeed = 0.05f;
    public float TraverseSlowSpeed = 0.002f;
    public float SlowSpeedStep = 0.001f;
    public float MinSlowSpeed = 0.001f;
    public float MaxSlowSpeed = 0.1f;
    public float GearRotationSpeed = 110f;

    // --- Hydraulic Config ---
    [Header("Hydraulic Config")]
    public GameObject HydroAssemblyRoot;
    public List<Transform> HydroAssemblyBellowBones;

    [Header("Hydro Parameters")]
    public float HydroReturnDuration = 2f;
    public float HydraulicBufferMoveDuration = 0.5f;
    public float HydroFastSpeedChange = 0.02f;
    public float HydroSlowSpeedChange = 0.002f;
    public float HydroUpperLimitOffset = 0.2f;

    // --- Door Config ---
    [Header("Door Config")]
    public List<GameObject> DoorObjects;
    public float DoorOpenAngle = 110f;
    public float DoorAnimationDuration = 0.5f;

    // --- Clamping Config ---
    [Header("Clamping Config")]
    public Transform UpperHydroPiston;
    public Transform UpperLeftClampZone;
    public Transform UpperRightClampZone;
    public Transform LowerHydroPiston;
    public Transform LowerLeftClampZone;
    public Transform LowerRightClampZone;

    [Header("Clamping Parameters")]
    public float ClampDuration = 0.75f;
    public float PistonVerticalDisplacement = 0.05f;
    public float ZoneHorizontalDisplacement = 0.02f;

    // --- Visuals ---
    [Header("Visuals")]
    public List<Transform> ManometerNeedles;
    public float NeedleActiveAngle = 75f;
    public float NeedleAnimationDuration = 0.5f;

    public override IMachineLogic CreateLogic()
    {
        var logic = new HydraulicMachineLogic();
        // Передаем "себя" (конфиг) в логику для инициализации
        logic.Initialize(this); 
        return logic;
    }
}