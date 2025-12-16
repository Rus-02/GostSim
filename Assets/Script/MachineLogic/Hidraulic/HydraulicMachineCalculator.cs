using UnityEngine;

public class HydraulicMachineCalculator : IMachineCalculator
{
    // Нам больше не нужен enum Axis для нового метода, но оставляем для совместимости,
    // если CalculateDynamicLimits пока работает по-старому.
    public enum Axis { X, Y, Z }
    private readonly Axis _motionAxis;

    public HydraulicMachineCalculator(Axis axis = Axis.Z) 
    {
        _motionAxis = axis;
    }

    // --- Метод 1: Лимиты (Пока оставляем как было, если не меняли интерфейс) ---
    public (float NewMin, float NewMax) CalculateDynamicLimits(SystemStateMonitor monitor, Vector3 drivePos_world, Vector3 undrivePos_world)
    {
        // Примечание: Этот метод всё еще "грязный" (лезет в MachineController). 
        // Чтобы закрыть Фазу 1 полностью, его тоже надо будет перевести на "чистую" схему позже.
        // Но сейчас исправляем ошибку компиляции.
        
        Transform traverse = MachineController.Instance.MovingTraverseRoot;
        Transform railRoot = traverse.parent;

        if (railRoot == null) return (monitor.OriginMinLimitY, monitor.OriginMaxLimitY);

        Vector3 driveLocal = railRoot.InverseTransformPoint(drivePos_world);
        Vector3 undriveLocal = railRoot.InverseTransformPoint(undrivePos_world);
        Vector3 traverseLocal = traverse.localPosition;

        float driveVal = GetAxisValue(driveLocal);
        float undriveVal = GetAxisValue(undriveLocal);
        float currentTraverseVal = GetAxisValue(traverseLocal);

        monitor.CurrentSampleParameters.TryGetValue("Length", out float sampleLen_mm);
        float sampleLen = sampleLen_mm / 1000f;
        
        SampleData sData = SampleManager.Instance.GetFirstCompatibleSampleData(monitor.CurrentTestConfig, monitor.SelectedShape);
        float clampLen = (sData != null) ? sData.ClampingLength / 1000f : 0f;
        
        float totalLen = sampleLen + (clampLen * 2);
        float offset = driveVal - currentTraverseVal;

        float baseMin = monitor.OriginMinLimitY;
        float baseMax = monitor.OriginMaxLimitY;
        float finalMin = baseMin;
        float finalMax = baseMax;

        float direction = Mathf.Sign(driveVal - undriveVal); 
        if (direction == 0) direction = 1;

        float targetPos = undriveVal + (totalLen * direction);
        float dynamicLimit = targetPos - offset;

        if (monitor.CurrentGeneralTestType == TestType.Compression)
        {
            finalMin = Mathf.Max(baseMin, dynamicLimit);
        }
        else if (monitor.CurrentGeneralTestType == TestType.Tensile)
        {
            finalMax = Mathf.Min(baseMax, dynamicLimit);
        }

        return (finalMin, finalMax);
    }

    // Реализуем недостающий член интерфейса
    public float CalculateApproachTargetLocalScalar(ApproachCalculationArgs args)
    {
        // 1. Ищем геометрическую разницу
        Vector3 diff = args.DrivePosLocal - args.UndrivePosLocal;

        // 2. Определяем доминирующую ось (X, Y или Z), игнорируя знак
        // Если разница мизерная (точки совпадают), берем ось из конфига.
        Vector3 effectiveAxis = args.LocalMotionAxis;

        float absX = Mathf.Abs(diff.x);
        float absY = Mathf.Abs(diff.y);
        float absZ = Mathf.Abs(diff.z);

        // Если точки разнесены в пространстве, выбираем ту ось, вдоль которой разброс больше всего.
        // И берем СТРОГО положительный вектор (1,0,0), (0,1,0) или (0,0,1).
        if (diff.sqrMagnitude > 1e-6f)
        {
            if (absX > absY && absX > absZ) effectiveAxis = Vector3.right;   // (1, 0, 0)
            else if (absY > absX && absY > absZ) effectiveAxis = Vector3.up; // (0, 1, 0)
            else effectiveAxis = Vector3.forward;                            // (0, 0, 1)
        }

        // 3. Проецируем позиции на эту "чистую" ось
        float driveVal = Vector3.Dot(args.DrivePosLocal, effectiveAxis);
        float undriveVal = Vector3.Dot(args.UndrivePosLocal, effectiveAxis);
        float traverseCenterVal = Vector3.Dot(args.TraverseCenterLocal, effectiveAxis);

        // 4. Стандартная математика
        float effectiveLen = args.EffectiveDimension_mm / 1000f;

        // Направление: +1 или -1 вдоль оси
        float direction = Mathf.Sign(driveVal - undriveVal);
        if (direction == 0) direction = 1;

        // Цель
        float targetDriveVal = undriveVal + (effectiveLen * direction);
        float offset = driveVal - traverseCenterVal;

        return targetDriveVal - offset;
    }

    // Хелпер для старого метода
    private float GetAxisValue(Vector3 v)
    {
        switch (_motionAxis)
        {
            case Axis.X: return v.x;
            case Axis.Y: return v.y;
            case Axis.Z: return v.z;
            default: return v.z;
        }
    }
}