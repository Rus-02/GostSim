using UnityEngine;

public static class CalculatorFactory
{
    public static IMachineCalculator Create(SystemStateMonitor monitor)
    {
        // Здесь будет выбор в зависимости от типа машины
        // MachineType machineType = monitor.CurrentMachineType;
        // switch (machineType) ...

        // А пока всегда возвращаем гидравлический
        return new HydraulicMachineCalculator();

        // В будущем:
        // default:
        //     Debug.LogError($"Не найден калькулятор для типа машины: {machineType}");
        //     return null;
    }
}