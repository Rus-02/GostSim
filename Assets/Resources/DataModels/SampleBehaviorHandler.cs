using UnityEngine;

public class SampleBehaviorHandler : MonoBehaviour
{
    [Header("Managed Parts (Assign in Prefab)")]
    [Tooltip("Часть(и) образца, которую нужно отсоединить/перепривязать при разрушении.")]
    [SerializeField] private GameObject partToManage;

    [Header("Machine Targets (Name - Assign in Prefab)")]
    [Tooltip("ИМЯ объекта-цели в сцене, к которому нужно прикрепиться (например, 'НижнийЗажим_AttachPoint'). Имя должно быть уникальным в сцене!")]
    [SerializeField] private string attachTargetName = "DefaultAttachPointName"; // Задайте осмысленный дефолт или оставьте пустым

    // --- Internal State ---
    private Transform initialParent;
    private Vector3 initialLocalPosition;
    private Quaternion initialLocalRotation;

    private bool isFailed = false;
    private bool initialStateSaved = false;

    public void HandleFailure()
    {
        if (isFailed) return;
        if (partToManage == null)
        {
            Debug.LogError($"[{this.GetType().Name}:{gameObject.name}] 'Part To Manage' не назначен в инспекторе!", this);
            return;
        }
        // Проверяем, задано ли имя цели
        if (string.IsNullOrEmpty(attachTargetName))
        {
            Debug.LogError($"[{this.GetType().Name}:{gameObject.name}] 'Attach Target Name' не задан в инспекторе!", this);
            return;
        }

        // --- ИЩЕМ ЦЕЛЬ В СЦЕНЕ ПО ИМЕНИ ИЗ ПОЛЯ ---
        GameObject attachTargetGO = GameObject.Find(attachTargetName);
        // -------------------------------------------

        if (attachTargetGO == null)
        {
            Debug.LogError($"[{this.GetType().Name}:{gameObject.name}] Не найден объект с именем '{attachTargetName}' в сцене!", this);
            return;
        }

        Transform attachTargetTransform = attachTargetGO.transform;

        isFailed = true;
        SaveInitialStateIfNeeded();
        partToManage.transform.SetParent(attachTargetTransform, true);
    }

    public void ResetBehavior()
    {
        Debug.Log($"<color=lightblue>[{this.GetType().Name}:{gameObject.name}] Resetting behavior.</color>");
        if (initialStateSaved && partToManage != null && initialParent != null)
        {
            if (partToManage.transform.parent != initialParent)
            {
                // Debug.Log($"Re-attaching '{partToManage.name}' to '{initialParent.name}'.");
                partToManage.transform.SetParent(initialParent, false);
                partToManage.transform.localPosition = initialLocalPosition;
                partToManage.transform.localRotation = initialLocalRotation;
            }
        }
        else if (partToManage != null && initialParent == null && initialStateSaved)
        {
            Debug.LogWarning($"[{this.GetType().Name}:{gameObject.name}] Initial parent was null, cannot re-attach '{partToManage.name}' automatically.");
        }

        isFailed = false;
        initialStateSaved = false;
    }

    private void SaveInitialStateIfNeeded()
    {
        if (initialStateSaved) return;
        if (partToManage == null) return;

        initialParent = partToManage.transform.parent;
        initialLocalPosition = partToManage.transform.localPosition;
        initialLocalRotation = partToManage.transform.localRotation;
        initialStateSaved = true;

        if (initialParent == null)
        {
            Debug.LogWarning($"[{this.GetType().Name}:{gameObject.name}] The initial parent of '{partToManage.name}' is null (already root?). Reset might not work as expected.");
        }
    }
    private void OnDisable()
    {
        ResetBehavior();
    }

}