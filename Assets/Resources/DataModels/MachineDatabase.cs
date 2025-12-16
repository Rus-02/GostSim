using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "MachineDatabase", menuName = "Simulation/Machine Database")]
public class MachineDatabase : ScriptableObject
{
    public List<MachineData> allMachines;
}