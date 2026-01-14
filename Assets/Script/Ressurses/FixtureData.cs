using UnityEngine;
using System.Collections.Generic;

public abstract class FixtureData : ScriptableObject
{
    public string fixtureId;
    public string displayName;
    [TextArea]
    public string description;
    public GameObject prefabModel;
    public FixtureZone fixtureZone;
    public string partNumber;
    [Header("Placement Logic")]
    
    public SamplePlacementSource placementSource = SamplePlacementSource.FixedOnMachine;

    // Эти поля используются, только если placementSource = OnFixtureType
    public string parentFixtureId;
    public string parentAttachmentPointName;
    
    [Header("Special Logic")]
    [Tooltip("Если true, эта оснастка будет автоматически удалена, если она не указана в списке целевой оснастки для нового теста.")]
    public bool isSpecializedEquipment = false;

    [Header("Animations")]
    public FixtureAnimationData InAnimation;
    public FixtureAnimationData OutAnimation;
    
    // НОВЫЕ ПОЛЯ ДЛЯ АНИМАЦИЙ ОБРАЗЦА
    public FixtureAnimationData SampleInstallAnimation;
    public FixtureAnimationData SampleRemoveAnimation;
    
    // Опционально: список для реально кастомных, редких случаев
    public List<FixtureAnimationData> CustomAnimations;

    public abstract string GetFixtureType();
    
}