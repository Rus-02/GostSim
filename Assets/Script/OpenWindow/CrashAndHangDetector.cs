using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

public class CrashAndHangDetector : MonoBehaviour
{
    // --- Настройки ---
    private const int MAX_LOGS_TO_KEEP = 150;
    private const float HANG_THRESHOLD_SECONDS = 7.0f; 
    private const int CHECK_INTERVAL_MS = 1500;

    // --- Ключи для сохранения ---
    private const string LOGS_PREFS_KEY = "BlackBox_RecentLogs";
    private const string SESSION_ACTIVE_PREFS_KEY = "BlackBox_SessionActiveFlag"; 
    private const string LOG_SEPARATOR = "|||LOG_SEPARATOR|||";

    // --- Внутреннее состояние ---
    private readonly Queue<string> _recentLogs = new Queue<string>();
    private static volatile float _lastHeartbeatTime = 0f;
    private static volatile bool _shutdownRequested = false;
    private static bool _isCleanExit = false;

    #region Singleton и Инициализация
    private static CrashAndHangDetector _instance;

    void Awake()
    {
        if (_instance != null) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        
        CheckForPreviousCrashOrHang();
        
        // Отмечаем, что НОВАЯ сессия началась
        PlayerPrefs.SetInt(SESSION_ACTIVE_PREFS_KEY, 1);
        PlayerPrefs.Save();

        LoadPersistedLogs();
    }

    void Start()
    {
        _lastHeartbeatTime = Time.realtimeSinceStartup;
        MonitorHeartbeat();
    }
    #endregion

    // Блок сбора логов 
    #region Сбор и сохранение логов
    void OnEnable() { Application.logMessageReceivedThreaded += HandleLog; }
    void OnDisable() { Application.logMessageReceivedThreaded -= HandleLog; }

    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        string formattedLog = $"[{type}] {Time.realtimeSinceStartup:F2}s | {logString}";
        lock (_recentLogs)
        {
            _recentLogs.Enqueue(formattedLog);
            while (_recentLogs.Count > MAX_LOGS_TO_KEEP)
            {
                _recentLogs.Dequeue();
            }
        }
    }
    
    void Update()
    {
        // Периодически сохраняем логи на диск
        if (Time.frameCount % 60 == 0)
        {
            SaveChanges();
        }

        if (_shutdownRequested)
        {
            // Этот флаг теперь нужен только для немедленного выхода, если зависание обнаружил наш наблюдатель
            Application.Quit();
        }
    }

    private void SaveChanges()
    {
        string logsToSave;
        lock (_recentLogs)
        {
            logsToSave = string.Join(LOG_SEPARATOR, _recentLogs);
        }
        PlayerPrefs.SetString(LOGS_PREFS_KEY, logsToSave);
        PlayerPrefs.Save(); // Сохраняем и логи, и флаг сессии
    }
    #endregion
    
    #region Детектор зависаний
    void LateUpdate()
    {
        _lastHeartbeatTime = Time.realtimeSinceStartup;
    }

    private async void MonitorHeartbeat()
    {
        while (true)
        {
            await Task.Delay(CHECK_INTERVAL_MS);
            if (Time.realtimeSinceStartup - _lastHeartbeatTime > HANG_THRESHOLD_SECONDS)
            {
                // Наше приложение зависло. Мы не можем ничего записать,
                // но флаг SESSION_ACTIVE_PREFS_KEY уже стоит в 1. Этого достаточно.
                // Просто инициируем аварийный выход, чтобы OS не показывала диалог ANR.
                _shutdownRequested = true;
                return;
            }
        }
    }
    #endregion

    #region Логика при выходе и следующем запуске
    
    public static void NotifyCleanExit()
    {
        _isCleanExit = true;
    }

    void OnApplicationQuit()
    {
        if (_isCleanExit)
        {
            // Если выход штатный - сбрасываем флаг активной сессии.
            PlayerPrefs.SetInt(SESSION_ACTIVE_PREFS_KEY, 0);
        }
        // Если выход НЕ штатный (зависание, вылет, закрытие из диспетчера) -
        // флаг останется равным 1, что мы и обнаружим при следующем запуске.

        SaveChanges();
    }

    private void CheckForPreviousCrashOrHang()
    {
        // Проверяем, осталась ли предыдущая сессия "активной".
        if (PlayerPrefs.GetInt(SESSION_ACTIVE_PREFS_KEY, 0) == 1)
        {
            // Если да - значит, был сбой.
            string savedLogs = PlayerPrefs.GetString(LOGS_PREFS_KEY, "No logs saved.");
            string path = Path.Combine(Application.persistentDataPath, $"crash_report_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");
            
            try
            {
                string reportContent = "--- CRASH OR HANG DETECTED IN PREVIOUS SESSION ---\n\n" + savedLogs.Replace(LOG_SEPARATOR, "\n");
                File.WriteAllText(path, reportContent);

                Debug.LogError("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                Debug.LogError("!!! ОБНАРУЖЕН СБОЙ В ПРЕДЫДУЩЕЙ СЕССИИ !!!");
                Debug.LogError($"!!! Отчет с логами сохранен в файл: {path}");
                Debug.LogError("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"!!! Не удалось сохранить файл отчета о сбое: {e.Message}");
            }
        }
    }

    private void LoadPersistedLogs()
    {
        string savedLogs = PlayerPrefs.GetString(LOGS_PREFS_KEY, "");
        if (!string.IsNullOrEmpty(savedLogs))
        {
            Debug.LogWarning("--- Восстановление логов из предыдущей сессии ---");
            string[] logs = savedLogs.Split(new[] { LOG_SEPARATOR }, System.StringSplitOptions.None);
            foreach (string log in logs)
            {
                Debug.Log($"[RESTORED] {log}");
            }
            Debug.LogWarning("--- Восстановление завершено ---");
        }
    }
    #endregion
}