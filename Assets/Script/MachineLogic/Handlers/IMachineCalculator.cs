using UnityEngine;

/// <summary>
/// Аргументы для чистого математического расчета.
/// Все позиции должны быть уже переведены в локальную систему координат рельс.
/// </summary>
public struct ApproachCalculationArgs
{
    public Vector3 DrivePosLocal;       // Локальная позиция точки (Drive)
    public Vector3 UndrivePosLocal;     // Локальная позиция точки (Undrive)
    public Vector3 TraverseCenterLocal; // Локальная позиция центра траверсы (для оффсета)
    
    public float EffectiveDimension_mm; // Размер образца
    public ApproachActionType ActionType; 
    
    public Vector3 LocalMotionAxis;     // Ось движения (из конфига), нормализованная
}

public interface IMachineCalculator
{
    // Расчет лимитов пока не трогаем (если хочешь, можно и его потом почистить),
    // но для текущей задачи нам важен этот метод:
    (float NewMin, float NewMax) CalculateDynamicLimits(SystemStateMonitor monitor, Vector3 drivePos_world, Vector3 undrivePos_world);    
    
    /// <summary>
    /// Чистая математика. Возвращает скалярное значение (число) целевой позиции на оси.
    /// </summary>
    float CalculateApproachTargetLocalScalar(ApproachCalculationArgs args);    
}
