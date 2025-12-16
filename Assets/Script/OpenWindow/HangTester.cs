using UnityEngine;

public class HangTester : MonoBehaviour
{
    // Этот метод повесит приложение намертво
    public void SimulateHang()
    {
        Debug.LogWarning("--- SIMULATING APP HANG NOW ---");
        while (true)
        {
            // Бесконечный цикл, который блокирует главный поток
        }
    }
}