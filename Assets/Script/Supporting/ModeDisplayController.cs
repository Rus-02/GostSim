using UnityEngine;
using UnityEngine.UI;

public class ModeDisplayController : MonoBehaviour
{
    public static ModeDisplayController Instance { get; private set; }

    [SerializeField] private Text modeTextComponent;

    private void Awake()
    {
        Instance = this;
        ToDoManager.Instance.SubscribeToAction(ActionType.SetDisplayMode, HandleSetDisplayModeAction);
    }

    private void OnDestroy()
    {
        if (ToDoManager.Instance != null)
        {
            ToDoManager.Instance.UnsubscribeFromAction(ActionType.SetDisplayMode, HandleSetDisplayModeAction);
        }
    }

    private void HandleSetDisplayModeAction(BaseActionArgs args)
    {
        if (args is SetDisplayModeArgs modeArgs)
        {
            modeTextComponent.text = modeArgs.ModeText;
        }
    }
}
