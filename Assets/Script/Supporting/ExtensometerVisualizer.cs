using UnityEngine;

public class ExtensometerVisualizer : MonoBehaviour
{
    // --- Внутреннее состояние ---
    private Vector3 _tablePosition;
    private Quaternion _tableRotation;
    private Vector3 _initialAttachPosition;
    public bool IsAttached { get; private set; } = false;

    // --- Управление подписками ---

    private void Start()
    {
        // Подписываемся на ЕДИНУЮ команду управления экстензометром
        if (ToDoManager.Instance != null)
        {
            ToDoManager.Instance.SubscribeToAction(ActionType.ControlExtensometer, HandleControlCommand);
        }
    }

    private void OnDestroy()
    {
        // Отписываемся, чтобы избежать утечек памяти
        if (ToDoManager.Instance != null)
        {
            ToDoManager.Instance.UnsubscribeFromAction(ActionType.ControlExtensometer, HandleControlCommand);
        }
    }

    // --- Инициализация

    private void Awake()
    {
        // Запоминаем свою стартовую позицию и поворот, заданные в редакторе.
        _tablePosition = transform.position;
        _tableRotation = transform.rotation;
    }

    // --- ЕДИНЫЙ ОБРАБОТЧИК КОМАНД ---
    
    /// Получает все команды, связанные с экстензометром, и распределяет их по нужным методам.
    private void HandleControlCommand(BaseActionArgs baseArgs)
    {
        // 1. Преобразуем аргументы в наш конкретный тип.
        var args = baseArgs as ExtensometerControlArgs;
        if (args == null) return; // Если это не наша команда, игнорируем.

        // 2. Используем switch для определения, какое действие нужно выполнить.
        switch (args.Action)
        {
            case ExtensometerAction.Attach:
                // Проверяем, что нам передали необходимые данные (точки крепления)
                if (args.DrivePoint != null && args.UndrivePoint != null)
                {
                    AttachToPoints(args.DrivePoint, args.UndrivePoint);
                }
                else
                {
                    Debug.LogError("[ExtensometerVisualizer] Получена команда Attach, но не переданы точки DrivePoint/UndrivePoint!");
                }
                break;

            case ExtensometerAction.ReturnToTable:
                ReturnToTable();
                break;

            case ExtensometerAction.UpdatePosition:
                // Проверяем, что нам передали необходимые данные (величину удлинения)
                if (args.Elongation_mm.HasValue)
                {
                    UpdatePosition(args.Elongation_mm.Value);
                }
                break;
        }
    }

    // --- МЕТОДЫ-ИСПОЛНИТЕЛИ ---

    private void AttachToPoints(Transform drive, Transform undrive)
    {
        _initialAttachPosition = Vector3.Lerp(drive.position, undrive.position, 0.5f);
        transform.position = _initialAttachPosition;
        
        Vector3 direction = (drive.position - undrive.position).normalized;
        if (direction != Vector3.zero) transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        transform.rotation *= Quaternion.Euler(90, 0, 0);

        IsAttached = true;
    }

    private void UpdatePosition(float totalElongation_mm)
    {
        if (!IsAttached) return;

        float halfElongation_m = (totalElongation_mm / 2.0f) / 1000.0f;
        transform.position = _initialAttachPosition + new Vector3(0, halfElongation_m, 0);
    }

    private void ReturnToTable()
    {
        transform.position = _tablePosition;
        transform.rotation = _tableRotation;
        IsAttached = false;
    }
}