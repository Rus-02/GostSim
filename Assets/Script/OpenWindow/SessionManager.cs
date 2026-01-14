using UnityEngine;
using UnityEngine.AddressableAssets;

/// <summary>
/// Хранит данные сессии (Addressables версия).
/// </summary>
public class SessionManager : MonoBehaviour
{
    public static SessionManager Instance { get; private set; }

    // Данные, которые мы хотим передать
    public float MaxMachineForce_kN { get; private set; } = float.MaxValue;
    
    // ВМЕСТО GameObject МЫ ХРАНИМ ССЫЛКУ (AssetReference)
    // Это позволяет не держать префаб в памяти при смене сцен.
    public AssetReferenceGameObject MachineReference { get; private set; } 

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Устанавливает данные для текущей сессии симуляции.
    /// Принимает AssetReferenceGameObject вместо GameObject.
    /// </summary>
    public void SetSessionData(float maxForce, AssetReferenceGameObject machineRef)
    {
        MaxMachineForce_kN = maxForce;
        MachineReference = machineRef;
        Debug.Log($"[SessionManager] Data set via Addressables: Ref={(machineRef != null ? "Valid" : "Null")}");
    }
}