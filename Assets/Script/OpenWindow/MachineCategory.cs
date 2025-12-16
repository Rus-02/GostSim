using System.Collections.Generic;

public enum MachineCategory { Electromechanical, Hydraulic, Press }

public static class MachineCategoryHelper
{
    // 1. Словарь для сопоставления enum'а и его названия на русском
    private static readonly Dictionary<MachineCategory, string> CategoryNames = new Dictionary<MachineCategory, string>
    {
        { MachineCategory.Electromechanical, "Машина электромеханическая" },
        { MachineCategory.Hydraulic, "Машина гидравлическая" },
        { MachineCategory.Press, "Пресс гидравлический" }
    };

    // 2. Список, который определяет ТОЧНЫЙ порядок категорий в выпадающем списке
    public static readonly List<MachineCategory> DisplayOrder = new List<MachineCategory>
    {
        MachineCategory.Electromechanical,
        MachineCategory.Hydraulic,
        MachineCategory.Press
    };

    // 3. Метод для получения "красивого" имени по значению enum
    public static string GetDisplayName(MachineCategory category)
    {
        // Если по какой-то причине имя не найдено, вернем стандартное, чтобы не было ошибки
        return CategoryNames.TryGetValue(category, out var name) ? name : category.ToString();
    }
}