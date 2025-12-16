using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using UnityEngine.SceneManagement;
using DG.Tweening;
using UnityEngine.InputSystem;

public class MachineSelectionManager : MonoBehaviour
{
    [Header("Database")]
    public MachineDatabase machineDatabase;

    [Header("UI Elements")]
    public TMP_Text machineNameText;
    public TMP_Text specsText;
    public TMP_Dropdown categoryDropdown;
    public Button nextButton;
    public Button prevButton;
    public Button startButton;
    public Button quitButton;
    [SerializeField] private Image loadingOverlay;

    [Header("Scene Objects")]
    public Transform machineContainer;

    [Header("Carousel Settings")]
    public float machineSpacing = 10f;
    public float animationDuration = 0.5f;

    private List<MachineData> filteredMachines;
    private List<GameObject> instantiatedMachines = new List<GameObject>();
    private int currentMachineIndex;
    private MachineCategory currentCategory;
    private Vector3 startContainerPosition;

    [Header("Swipe Controls")]
    [SerializeField] private float minSwipeDistance = 40f;

    private PlayerControls playerControls;
    private Vector2 swipeStartPosition;
    private bool isSwiping = false;

    void Awake()
    {
        playerControls = new PlayerControls();
    }

    private void OnEnable()
    {
        playerControls.Camera.Enable();
        playerControls.Camera.PrimaryTouchContact.started += OnSwipeStart;
        playerControls.Camera.PrimaryTouchContact.canceled += OnSwipeEnd;
    }

    private void OnDisable()
    {
        playerControls.Camera.PrimaryTouchContact.started -= OnSwipeStart;
        playerControls.Camera.PrimaryTouchContact.canceled -= OnSwipeEnd;
        playerControls.Camera.Disable();
    }

    void Update()
    {
        if (!isSwiping) return;

        Vector2 currentPosition = playerControls.Camera.PrimaryTouchPosition.ReadValue<Vector2>();
        Vector2 swipeDirection = currentPosition - swipeStartPosition;

        if (Mathf.Abs(swipeDirection.x) > minSwipeDistance)
        {
            if (Mathf.Abs(swipeDirection.x) > Mathf.Abs(swipeDirection.y))
            {
                if (swipeDirection.x < 0) ChangeMachine(1);
                else ChangeMachine(-1);
                isSwiping = false;
            }
        }
    }

    void Start()
    {
        if (loadingOverlay != null) { loadingOverlay.fillAmount = 0; }
        if (machineContainer != null) { startContainerPosition = machineContainer.position; }

        PopulateCategoryDropdown();
        categoryDropdown.onValueChanged.AddListener(OnCategoryChanged);
        nextButton.onClick.AddListener(() => ChangeMachine(1));
        prevButton.onClick.AddListener(() => ChangeMachine(-1));
        startButton.onClick.AddListener(StartSimulation);

        if (quitButton != null) { quitButton.onClick.AddListener(QuitApplication); }
        OnCategoryChanged(0);
    }

    void PopulateCategoryDropdown()
    {
        categoryDropdown.ClearOptions();
        var displayNames = MachineCategoryHelper.DisplayOrder
            .Select(category => MachineCategoryHelper.GetDisplayName(category))
            .ToList();
        categoryDropdown.AddOptions(displayNames);
    }

    void OnCategoryChanged(int dropdownIndex)
    {
        foreach (var machine in instantiatedMachines)
        {
            Destroy(machine);
        }
        instantiatedMachines.Clear();
        machineContainer.DOKill();
        machineContainer.position = startContainerPosition;

        currentCategory = MachineCategoryHelper.DisplayOrder[dropdownIndex];
        filteredMachines = machineDatabase.allMachines.Where(m => m.machineCategory == currentCategory).ToList();

        if (filteredMachines.Count == 0)
        {
            machineNameText.text = "";
            specsText.text = "";
            UpdateNavigationUI();
            return;
        }

        Vector3 rightDirection = -machineContainer.transform.forward;
        for (int i = 0; i < filteredMachines.Count; i++)
        {
            Vector3 spawnPosition = startContainerPosition + (rightDirection * i * machineSpacing);
            GameObject newMachine = Instantiate(filteredMachines[i].machinePrefab, spawnPosition, machineContainer.rotation, machineContainer);
            instantiatedMachines.Add(newMachine);
        }

        currentMachineIndex = 0;
        UpdateNavigationUI();
    }

    void ChangeMachine(int direction)
    {
        if (filteredMachines.Count <= 1) return;
        int nextIndex = currentMachineIndex + direction;
        if (nextIndex < 0 || nextIndex >= filteredMachines.Count) return;
        currentMachineIndex = nextIndex;
        Vector3 rightDirection = -machineContainer.transform.forward;
        Vector3 targetPosition = startContainerPosition - (rightDirection * currentMachineIndex * machineSpacing);
        machineContainer.DOMove(targetPosition, animationDuration).SetEase(Ease.OutCubic);
        UpdateNavigationUI();
    }

    void UpdateNavigationUI()
    {
        MachineData currentMachine = filteredMachines[currentMachineIndex];
        machineNameText.text = currentMachine.machineName;
        specsText.text = currentMachine.specs;
        startButton.gameObject.SetActive(currentMachine.isSimulationReady);
        bool hasNext = currentMachineIndex < filteredMachines.Count - 1;
        bool hasPrev = currentMachineIndex > 0;
        nextButton.gameObject.SetActive(hasNext);
        prevButton.gameObject.SetActive(hasPrev);
    }

    void StartSimulation()
    {
        MachineData selectedMachine = filteredMachines[currentMachineIndex];
        if (SessionManager.Instance != null)
        {
            SessionManager.Instance.SetSessionData(selectedMachine.maxForce_kN, selectedMachine.machinePrefab);
        }
        else
        {
            Debug.LogError("[MachineSelectionManager] SessionManager не найден на сцене!");
        }
        StartCoroutine(LoadMenuSceneAsync());
    }

    private IEnumerator LoadMenuSceneAsync()
    {
        nextButton.interactable = false;
        prevButton.interactable = false;
        startButton.interactable = false;
        categoryDropdown.interactable = false;

        AsyncOperation asyncOperation = SceneManager.LoadSceneAsync("Menu");
        asyncOperation.allowSceneActivation = false;

        while (!asyncOperation.isDone)
        {
            float progress = Mathf.Clamp01(asyncOperation.progress / 0.9f);
            loadingOverlay.fillAmount = progress;
            if (asyncOperation.progress >= 0.9f)
            {
                loadingOverlay.fillAmount = 1f;
                asyncOperation.allowSceneActivation = true;
            }
            yield return null;
        }
    }

    private void OnSwipeStart(InputAction.CallbackContext context)
    {
        isSwiping = true;
        swipeStartPosition = playerControls.Camera.PrimaryTouchPosition.ReadValue<Vector2>();
    }

    private void OnSwipeEnd(InputAction.CallbackContext context)
    {
        isSwiping = false;
    }

    // Метод для закрытия приложения
    private void QuitApplication()
    {
        Debug.Log("Запрос на выход из приложения...");
        CrashAndHangDetector.NotifyCleanExit();
        Application.Quit();

        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
}