using System.Collections;
using UnityEngine;

public enum MonitorCheckType 
{ 
    IsSampleInPlace, 
    IsDoorClosed, 
    IsPumpOn,
    IsExtensometerEnabled,
    WasExtensometerAttachRequested, 
    WasExtensometerRemoveRequested,
    // ... добавлять сюда новые по мере надобности
}

[CreateAssetMenu(fileName = "Cond_NewCheck", menuName = "Scenario/Conditions/Check Monitor")]

public class CheckMonitorCondition : InputCondition
{
    public MonitorCheckType CheckType;
    public bool TargetValue = true;

    public override bool IsMet()
    {
        var m = SystemStateMonitor.Instance;
        switch (CheckType)
        {
            case MonitorCheckType.IsSampleInPlace: return m.IsSampleInPlace == TargetValue;
            case MonitorCheckType.IsDoorClosed: return m.AreDoorsClosed == TargetValue;
            case MonitorCheckType.IsPumpOn: return m.IsPowerUnitActive == TargetValue;
            case MonitorCheckType.IsExtensometerEnabled: return m.IsExtensometerEnabledByUser == TargetValue;
            case MonitorCheckType.WasExtensometerAttachRequested: return m.WasExtensometerAttachRequested == TargetValue;
            case MonitorCheckType.WasExtensometerRemoveRequested: return m.WasExtensometerRemoveRequested == TargetValue;
            // ...
        }
        return false;
    }
}