// ReportConfiguration.cs
using UnityEngine;
using System.Collections.Generic;

// Абстрактный базовый класс для конфигурации отчета.
// Каждый конкретный стандарт (ГОСТ) будет иметь свой наследник от этого класса.
public abstract class ReportConfiguration : ScriptableObject
{
    [Header("Основные настройки")]
    [Tooltip("Название отчета/ГОСТа, например 'ГОСТ 1497-84' или 'ГОСТ 10180-90'")]
    public string ReportName;

    [Tooltip("Ссылка на ассет конфигурации теста, с которым связан этот отчет.")]
    public TestConfigurationData LinkedTest; // <-- Поле для привязки к тесту

    [Header("Настройки таблицы (Подробный отчет)")]
    [Tooltip("Список столбцов для таблицы в большом (полном) отчете. Порядок в списке определяет порядок столбцов.")]
    public List<TableColumn> TableColumns = new List<TableColumn>(); // Список столбцов таблицы

    [Header("Настройки краткого отчета")]
    [Tooltip("Список параметров, отображаемых в маленьком (кратком) отчете.")]
    public List<ShortReportField> ShortReportFields = new List<ShortReportField>(); 
    public float ExtensometerBaseLength_mm;

    // Абстрактный метод для создания экземпляра соответствующих данных отчета.
    // Это позволяет ReportManager создавать правильный тип данных.
    public abstract ReportData CreateReportData();
}

// Класс, описывающий один столбец таблицы в ПОЛНОМ отчете.
[System.Serializable]
public class TableColumn
{
    [Tooltip("Заголовок столбца, отображаемый в UI (например 'Предел текучести, МПа').")]
    public string HeaderText; // Заголовок столбца

    [Tooltip("Ключ (имя свойства) для получения данных из ReportData через Reflection (например 'YieldStrength_MPa').")]
    public string DataKey; // Ключ для получения данных из ReportData

    [Tooltip("Строка формата для отображения числовых данных (например, 'F2', 'F1', 'N0').")]
    public string Format = "F2"; // Формат отображения данных, значение по умолчанию
}

// Класс, описывающий один параметр в КРАТКОМ отчете.
[System.Serializable]
public class ShortReportField
{
    [Tooltip("Метка параметра, отображаемая в UI (например 'Предел текучести:').")]
    public string Label; // Метка параметра

    [Tooltip("Ключ (имя свойства) для получения данных из ReportData через Reflection (например 'YieldStrength_MPa').")]
    public string DataKey; // Ключ для получения данных из ReportData

    [Tooltip("Строка формата для отображения числовых данных (например, 'F2', 'F1', 'N0').")]
    public string Format = "F2"; // Формат отображения данных, значение по умолчанию
}

// Этот конкретный класс нужен только для создания базового ассета через меню.
// Для каждого ГОСТа будет создаваться свой собственный наследник.
[CreateAssetMenu(fileName = "NewReportConfiguration", menuName = "Data Models/ReportConfigurations/Base Configuration")]
public class BaseReportConfiguration : ReportConfiguration
{
    // Реализация абстрактного метода.
    // Для базового ассета мы просто возвращаем базовый класс данных.
    public override ReportData CreateReportData()
    {
        // Возвращаем экземпляр оригинального класса данных из старого ReportManager.
        return new ReportData();
    }
}