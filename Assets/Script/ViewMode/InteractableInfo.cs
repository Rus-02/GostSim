using UnityEngine;
using System.Collections.Generic;
using System;

public class InteractableInfo : MonoBehaviour
{
    [Header("Identification & Linking")]
    [Tooltip("Уникальный идентификатор этого интерактивного элемента. Генерируется автоматически.")]
    [SerializeField] // Оставляем приватным, чтобы случайно не изменить, но видим в инспекторе
    private string identifier = Guid.NewGuid().ToString();

    [Tooltip("(Опционально) Ключ системного сообщения из PromptController. " +
             "Если указан, PromptController будет использовать его для поиска данных PromptData " +
             "в своем списке, ИГНОРИРУЯ Short/Detailed Description и Buttons, определенные ниже в этом компоненте. " +
             "Используется, если объект должен триггерить стандартный системный промпт.")]
    public string SystemPromptKey = ""; // Публичное поле для поиска из PromptController

    [Header("Display Content (если SystemPromptKey ПУСТОЙ)")]
    [Tooltip("Короткий текст для свернутого состояния панели.")]
    [TextArea(2, 5)]
    public string shortDescription = "Короткое описание";

    [Tooltip("Детальный текст для развернутого состояния панели.")]
    [TextArea(3, 10)]
    public string detailedDescription = "Детальное описание";

    [Header("Buttons (если SystemPromptKey ПУСТОЙ)")]
    [Tooltip("Кнопки для развернутого состояния панели (используются, если SystemPromptKey пустой).")]
    public List<ButtonData> buttonDataList = new List<ButtonData>();

    // --- Секция для данных оснастки (бывший XrayFixtureInfo) ---
    [Header("Fixture Specific Data (опционально, для оснастки)")]
    [Tooltip("Заполните, если этот объект является ОСНАСТКОЙ (Fixture).")]
    public bool isFixture = false; // Флаг для удобства в инспекторе и в коде

    [Tooltip("Отображаемое имя ТИПА оснастки (например, 'Вкладыши 9-14мм'). " +
             "Используется для групповой подсветки и логики смены. " +
             "Актуально, только если 'Is Fixture' отмечено.")]
    public string FixtureTypeDisplayName = "Неопределенный Тип Оснастки";

    [Tooltip("Список ассетов FixtureData, относящихся к этому ТИПУ оснастки. " +
             "Актуально, только если 'Is Fixture' отмечено.")]
             
    public List<FixtureData> associatedFixtureDataAssets = new List<FixtureData>();
    [Header("Context Menu")]
    [Tooltip("Список ключей для действий в контекстном меню. Эти ключи должны совпадать с ключами, настроенными в ContextMenuManager.")]
    public List<string> contextMenuKeys = new List<string>();

    public string Identifier => identifier;


    private void Awake()
    {
        // Гарантируем, что ID всегда есть
        if (string.IsNullOrEmpty(identifier))
        {
            Debug.LogWarning($"InteractableInfo на GameObject '{gameObject.name}' имеет пустой identifier! Назначается новый GUID.", this);
            identifier = Guid.NewGuid().ToString();
        }
    }

    private void OnValidate()
    {
        // Гарантируем, что ID всегда есть при изменениях в редакторе
        if (string.IsNullOrEmpty(identifier))
        {
            identifier = Guid.NewGuid().ToString();
        }
    }
    public bool HasValidFixtureData()
    {
        return isFixture &&
               (!string.IsNullOrEmpty(FixtureTypeDisplayName) && FixtureTypeDisplayName != "Неопределенный Тип Оснастки" ||
                associatedFixtureDataAssets.Count > 0);
    }

    private void OnDestroy() { }
}