using UnityEngine;

/// <summary>
/// Хранит данные сессии, которые должны сохраняться между сценами.
/// Использует паттерн Синглтон с DontDestroyOnLoad.
/// </summary>
public class SessionManager : MonoBehaviour
{
    public static SessionManager Instance { get; private set; }

    // Данные, которые мы хотим передать
    public float MaxMachineForce_kN { get; private set; } = float.MaxValue;
    public GameObject MachinePrefab { get; private set; }

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
    /// </summary>
    /// <param name="maxForce">Максимальная сила машины в кН.</param>
    /// <param name="prefab">Префаб 3D-модели машины.</param>
    public void SetSessionData(float maxForce, GameObject prefab)
    {
        MaxMachineForce_kN = maxForce;
        MachinePrefab = prefab;
        Debug.Log($"[SessionManager] Data set: Max Force = {maxForce} kN.");
    }
}