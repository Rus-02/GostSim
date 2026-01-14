using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "ProportionalAdapter_New", menuName = "Data Models/Fixtures/Proportional Adapter", order = 10)]
public class ProportionalAdapterData : FixtureData
{
    [Header("Параметры адаптера")]
    
    [Tooltip("Диаметр 'хвоста' этого адаптера в мм. Стандартные вкладыши должны быть способны зажать этот размер.")]
    public float adapterTailDiameter_mm;

    [Tooltip("Имена дочерних GameObjects в префабе адаптера, которые служат точками крепления для пропорциональных вкладышей.")]
    public List<string> internalAttachmentPointNames = new List<string>();

    public override string GetFixtureType()
    {
        return "ProportionalAdapter";
    }
}