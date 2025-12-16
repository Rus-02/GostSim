using UnityEngine;

[CreateAssetMenu(fileName = "New MachineData", menuName = "Simulation/Machine Data")]
public class MachineData : ScriptableObject
{
    public string machineName = "Новая машина";
    public MachineCategory machineCategory;
    public GameObject machinePrefab; // 3D модель для меню
    [Header("Technical Specifications")]
    public float maxForce_kN = 1000f;

    [TextArea(3, 5)]
    public string specs = "Масса: ...\nМакс. нагрузка: ...";
    public bool isSimulationReady = false; // Готов ли симулятор
}
