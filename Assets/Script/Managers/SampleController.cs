using UnityEngine;
using System.Collections.Generic;
using System;

public class SampleController : MonoBehaviour
{
    public SampleData sampleData;
    public List<Animator> ArmatureAnimators { get; private set; } = new List<Animator>();

    private GameObject _sampleInstance;
    private List<GameObject> _stageMeshes;

    public void SetupAndScaleSample(SampleData data, float targetDiameterThickness, float targetWidth, float targetLength)
    {
        // 1. Сохраняем данные
        this.sampleData = data;

        if (this.sampleData == null)
        {
            Debug.LogError("[SampleController] SetupAndScaleSample - SampleData is null!");
            return;
        }

        // 2. Рассчитать и применить масштаб
        Vector3 baseDimensions = this.sampleData.GetPrefabBaseDimensions();

        // 3. Рассчитать ПОЛНУЮ ВИЗУАЛЬНУЮ ДЛИНУ
        float workingLength = targetLength;
        float clampingLength = this.sampleData.ClampingLength;
        float totalVisualLength = workingLength + (clampingLength * 2);

        float totalVisualLengthMeters = totalVisualLength / 1000.0f;
        float targetDiameterThicknessMeters = targetDiameterThickness / 1000.0f;
        float targetWidthMeters = targetWidth / 1000.0f;
        // -------------------------------------------------

        float scaleX = 1f, scaleY = 1f, scaleZ = 1f;

        // Расчет scaleY (Длина)
        if (Mathf.Abs(baseDimensions.y) > Mathf.Epsilon) scaleY = totalVisualLengthMeters / baseDimensions.y; // Используем метры
        else Debug.LogWarning($"[TestController] Base Y dimension (Length) is close to zero in SampleData '{this.sampleData.sampleId}'! Scale Y set to 1.");

        // Расчет scaleX и scaleZ (Сечение)
        if (this.sampleData.sampleForm == SampleForm.Круг) // Используем sampleForm из полученного sampleData
        {
            // Для круга
            if (Mathf.Abs(baseDimensions.x) > Mathf.Epsilon) scaleX = targetDiameterThicknessMeters / baseDimensions.x; // Используем метры
            else Debug.LogWarning($"[TestController] Base X dimension (Diameter) is close to zero in SampleData '{this.sampleData.sampleId}'! Scale X set to 1.");
            if (Mathf.Abs(baseDimensions.z) > Mathf.Epsilon) scaleZ = targetDiameterThicknessMeters / baseDimensions.z; // Используем метры
            else Debug.LogWarning($"[TestController] Base Z dimension (Diameter) is close to zero in SampleData '{this.sampleData.sampleId}'! Scale Z set to 1.");
        }
        else // Прямоугольник/Квадрат
        {
            if (Mathf.Abs(baseDimensions.x) > Mathf.Epsilon) scaleX = targetWidthMeters / baseDimensions.x; // Используем метры
            else Debug.LogWarning($"[TestController] Base X dimension (Width) is close to zero in SampleData '{this.sampleData.sampleId}'! Scale X set to 1.");
            if (Mathf.Abs(baseDimensions.z) > Mathf.Epsilon) scaleZ = targetDiameterThicknessMeters / baseDimensions.z; // Используем метры
            else Debug.LogWarning($"[TestController] Base Z dimension (Thickness) is close to zero in SampleData '{this.sampleData.sampleId}'! Scale Z set to 1.");
        }


        // Применение масштаба к инстансу
        if (scaleX > 0 && scaleY > 0 && scaleZ > 0)
        {
            transform.localScale = new Vector3(scaleX, scaleY, scaleZ);
        }
        else
        {
            Debug.LogWarning($"[TestController] Calculated non-positive scale for {gameObject.name}: X={scaleX}, Y={scaleY}, Z={scaleZ}. Using default scale (1,1,1).");
            transform.localScale = Vector3.one; // Ставим дефолтный масштаб при ошибке
        }

    }

    public void VisualizeSample(SampleData data)
    {
        sampleData = data;

        if (sampleData == null || sampleData.prefabModel == null)
        {
            Debug.LogError("[SampleVisualizer] SampleData or Prefab Model is null!");
            return;
        }
        _sampleInstance = gameObject;
        Debug.Log($"[VisualizeSample] Экземпляр привязан: {_sampleInstance.name}, Родитель: {_sampleInstance.transform.parent?.name}");
    }

    private List<GameObject> FindStageMeshes()
    {
        List<GameObject> meshes = new List<GameObject>();
        // Ищем рекурсивно во всех дочерних объектах
        FindMeshesRecursively(transform, meshes);

        // Изначально скрываем все меши, кроме первого (если он есть)
        for(int i = 0; i < meshes.Count; i++)
        {
            meshes[i].SetActive(i == 0);
        }
        return meshes;
    }

    // Рекурсивный хелпер для поиска мешей
    private void FindMeshesRecursively(Transform parent, List<GameObject> meshes)
    {
        foreach (Transform child in parent)
        {
            if (child.name.Contains("Mesh_Stage")) // Ищем объекты с "Mesh_Stage" в имени
            {
                meshes.Add(child.gameObject);
            }
            if (child.childCount > 0)
            {
                FindMeshesRecursively(child, meshes);
            }
        }
    }

    private void ApplySampleDimensions() { }

    public void ShowSampleStage(int stageIndex)
    {
        if (_stageMeshes == null || stageIndex < 0 || stageIndex >= _stageMeshes.Count) return;

        // Деактивируем все меши
        foreach (var mesh in _stageMeshes)
        {
            mesh.SetActive(false);
        }
        // Активируем нужный меш
        _stageMeshes[stageIndex].SetActive(true);
    }
}