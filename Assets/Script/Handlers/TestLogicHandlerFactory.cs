using UnityEngine; // Оставим на всякий случай для Debug.Log

public static class TestLogicHandlerFactory
{
    // Главный метод, который используют все части системы.
    public static ITestLogicHandler Create(TestConfigurationData config)
    {
        if (config == null)
        {
            Debug.LogError("[TestLogicHandlerFactory] Config is null! Returning DefaultLogicHandler.");
            return new DefaultLogicHandler(null);
        }

        // --- Определяем хендлер строго по TypeOfTest ---
        switch (config.typeOfTest)
        {
            // --- Типы испытаний на РАСТЯЖЕНИЕ ---
            case TypeOfTest.WedgeGrip_Cylinder:
            case TypeOfTest.WedgeGrip_Flat:
                return new TensileLogicHandler(config);
            
            // --- Типы испытаний на СЖАТИЕ ---
            case TypeOfTest.Compression_Base160:
                return new CompressionLogicHandler(config);

            // --- Типы испытаний Пропорциональные ---
            case TypeOfTest.Proportional:
                return new TensileProportionalLogicHandler(config);


            // --- ОБРАБОТКА ПО УМОЛЧАНИЮ ---
            default:
                Debug.LogWarning($"[TestLogicHandlerFactory] Не найден специфичный обработчик для TypeOfTest: '{config.typeOfTest}'. Возвращен DefaultLogicHandler.");
                return new DefaultLogicHandler(config);
        }
    }

    // --- Метод-заглушка для ОБРАТНОЙ СОВМЕСТИМОСТИ ---
    public static ITestLogicHandler Create(TestType testType, TestConfigurationData config)
    {
        return Create(config);
    }
}
