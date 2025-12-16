using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// Хранит фиктивные параметры и умеет "подготавливать" (заполнять)
/// SystemStateMonitor для корректной работы TestLogicHandler в режиме просмотра (VSM).
/// </summary>
public class FictiveTestParameters : MonoBehaviour
{
    [Header("Параметры для заполнения Монитора")]
    public SampleForm SampleShape = SampleForm.Круг;
    public float SampleDiameterOrThickness = 10.0f;
    public float SampleWidth = 10.0f;
    public TypeOfTest TestTypeEnum = TypeOfTest.WedgeGrip_Cylinder;
    public List<string> AssociatedFixtureIDs = new List<string>();

    [Header("Параметры для проверки пространства")]
    public TestType GeneralTestType = TestType.Tensile;
    public float SafeTraversePositionLocalZ = -0.4f;

    [Header("Ссылка на реальную конфигурацию")]
    [Tooltip("Имя шаблона ИЗ ПАНЕЛИ НАСТРОЙКИ, которое соответствует этому набору оснастки. Используется для поиска полного TestConfigurationData.")]
    public string CorrespondingTemplateName;

    /// <summary>
    /// ГЛАВНЫЙ МЕТОД. Заполняет SystemStateMonitor фиктивными данными и создает
    /// на их основе корректный TestLogicHandler и его конфигурацию.
    /// </summary>
    public (ITestLogicHandler handler, TestConfigurationData config) PrimeMonitorAndCreateHandler()
    {
        var monitor = SystemStateMonitor.Instance;
        if (monitor == null || DataManager.Instance == null) return (null, null);

        // 1. НАХОДИМ НАСТОЯЩИЙ, ПОЛНЫЙ "ЧЕРТЕЖ" в DataManager
        TestConfigurationData realConfig = DataManager.Instance.AllTestConfigs.FirstOrDefault(
            t => t.templateName == this.CorrespondingTemplateName &&
                 t.compatibleSampleIDs.Any(id => 
                 {
                     var sampleData = DataManager.Instance.GetSampleDataByID(id);
                     return sampleData != null && sampleData.sampleForm == this.SampleShape;
                 })
        );

        if (realConfig == null)
        {
            Debug.LogError($"[FictiveTestParameters] Не удалось найти реальный TestConfigurationData для шаблона '{this.CorrespondingTemplateName}' и формы '{this.SampleShape}'!");
            return (null, null);
        }

        // 2. Заполняем Монитор фиктивными данными, как и раньше
        monitor.ReportSetupSelection(realConfig.templateName, null, this.SampleShape); // Используем имя из реального конфига
        var fictiveParams = new Dictionary<string, float>
        {
            { "DiameterThickness", this.SampleDiameterOrThickness },
            { "Width", this.SampleWidth }
        };
        monitor.ReportSampleParameters(fictiveParams, 0f);

        // 3. Создаем хендлер, используя ПОЛНЫЙ, НАСТОЯЩИЙ конфиг
        ITestLogicHandler handler = TestLogicHandlerFactory.Create(realConfig);

        // 4. Возвращаем хендлер и НАСТОЯЩИЙ конфиг
        return (handler, realConfig);
    }


    /// <summary>
    /// Стирает фиктивные данные об образце из Монитора.
    /// </summary>
    public void ResetMonitor()
    {
        // Вызываем ваш идеальный метод для очистки.
        SystemStateMonitor.Instance?.ResetTestSetupState();
        Debug.Log("[FictiveTestParameters] Фиктивные данные об образце стерты из Монитора.");
    }
    
    private TestConfigurationData CreateTemporaryTestConfig()
    {
        // 1. Создаем базу временного конфига
        TestConfigurationData tempConfig = ScriptableObject.CreateInstance<TestConfigurationData>();
        tempConfig.name = "TempConfig_For_VSM_Preview";
        tempConfig.typeOfTest = this.TestTypeEnum;
        tempConfig.testType = this.GeneralTestType;

        // 2. Начинаем собирать ПОЛНЫЙ список необходимой оснастки
        var allRequiredFixtures = new HashSet<string>(this.AssociatedFixtureIDs);

        // 3. Находим "старого" ответственного и просим его список деталей
        var activeHandler = FixtureController.Instance?.GetActiveLogicHandler();
        if (activeHandler != null)
        {
            // Получаем конфиг, с которым работал старый хендлер
            var oldConfig = SystemStateMonitor.Instance?.CurrentTestConfig; 
            if (oldConfig != null && oldConfig.potentialFixtureIDs != null)
            {
                // Добавляем все детали из старого конфига в наш общий список
                foreach(var id in oldConfig.potentialFixtureIDs)
                {
                    allRequiredFixtures.Add(id);
                }
            }
        }
        
        // 4. Записываем ОБЪЕДИНЕННЫЙ список в наш временный конфиг
        tempConfig.potentialFixtureIDs = allRequiredFixtures.ToList();

        return tempConfig;
    }
}