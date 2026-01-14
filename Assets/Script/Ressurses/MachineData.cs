using UnityEngine;
using UnityEngine.AddressableAssets;

[CreateAssetMenu(fileName = "New MachineData", menuName = "Simulation/Machine Data")]
public class MachineData : ScriptableObject
{
    public string machineName = "Новая машина";
    public MachineCategory machineCategory;

    [Header("Old System (For Carousel UI)")]
    [Tooltip("Префаб для отображения в карусели меню. Оставляем прямую ссылку.")]
    public GameObject machinePrefab; 

    [Header("Addressables (For Simulation)")]
    [Tooltip("Ссылка на Addressable-ассет для загрузки в сцене симуляции. Экономит память.")]
    public AssetReferenceGameObject machineAssetReference;

    [Header("Technical Specifications")]
    public float maxForce_kN = 1000f;

    [TextArea(3, 5)]
    public string specs = "Масса: ...\nМакс. нагрузка: ...";
    public bool isSimulationReady = false; 
}
