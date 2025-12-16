using UnityEngine;
using TMPro;
using UnityEngine.Networking;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.EventSystems;

public class LicenseChecker : MonoBehaviour
{
    public TMP_InputField keyInput;
    public TMP_Text statusText;
    public GameObject licenseWindow;
    public Button activateButton;
    public Button exitButton;
    public GameObject blockingOverlayContainer;
    private string deviceId;
    private Coroutine sizeChangeCoroutine;

    // --- ИЗМЕНЕНИЕ 1: Добавляем поле product_name в класс запроса ---
    [System.Serializable]
    public class LicenseRequest
    {
        public string key;
        public string device_id;
        public string product_name; // Новое поле для имени продукта
    }

    [System.Serializable]
    public class LicenseResponse
    {
        public string status;
        public string message;
        public string intended_version;
        public string intended_product_name; // Потенциально полезно для отладки или более сложных сценариев
    }

    void Start()
    {
        deviceId = SystemInfo.deviceUniqueIdentifier;
        if (string.IsNullOrEmpty(deviceId))
        {
            Debug.LogError("Не удалось получить идентификатор устройства!");
            statusText.text = "Ошибка: не удалось получить идентификатор устройства.";
            statusText.gameObject.SetActive(true);
            return;
        }

        keyInput.onValueChanged.AddListener(OnKeyInputChange);
        string savedKey = PlayerPrefs.GetString("license_key", "");
        statusText.gameObject.SetActive(false);
        blockingOverlayContainer.SetActive(false);

        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            Debug.Log("Интернет отсутствует");
            if (!string.IsNullOrEmpty(savedKey))
            {
                ShowStatusText("Нет интернета, работает пробный период 60 дней.");
                ShowTrialPeriodDays();
            }
            else
            {
                ShowLicenseWindow();
                ShowStatusText("Нет интернета, для активации необходим интернет.");
            }
        }
        else
        {
            Debug.Log("Интернет доступен");
            if (!string.IsNullOrEmpty(savedKey))
            {
                StartCoroutine(PerformLicenseCheck(savedKey, true));
            }
            else
            {
                ShowLicenseWindow();
            }
        }
        activateButton.onClick.AddListener(OnActivateButtonClick);
    }

    public void OnActivateButtonClick()
    {
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            ShowStatusText("Для активации нужен интернет");
            return;
        }
        string key = keyInput.text;
        if (string.IsNullOrEmpty(key))
        {
            Application.OpenURL("https://gost-lab.com/contacts");
        }
        else
        {
            StartCoroutine(PerformLicenseActivation(key));
        }
    }

    private IEnumerator PerformLicenseCheck(string key, bool isBackgroundCheck)
    {
        string url = "https://gostdev.ru:8443/check_license";

        // --- ИЗМЕНЕНИЕ 2: Формируем запрос с product_name ---
        string currentProductName = Application.productName;
        LicenseRequest requestData = new LicenseRequest {
            key = key,
            device_id = deviceId,
            product_name = currentProductName
        };
        string json = JsonUtility.ToJson(requestData);
        // --- Конец ИЗМЕНЕНИЯ 2 ---

        using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.SetRequestHeader("app-version", Application.version);
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"Сырой ответ от сервера (проверка успех): {www.downloadHandler.text}");
                var response = JsonUtility.FromJson<LicenseResponse>(www.downloadHandler.text);
                Debug.Log($"CheckLicense Result: Status={response.status}, Message={response.message}");

                if (response.status == "valid")
                {
                    if (!isBackgroundCheck) ShowStatusText("Лицензия действительна");
                    PlayerPrefs.SetString("license_key", key);
                    PlayerPrefs.SetInt("isLicensed", 1);
                    HideLicenseWindowAfterDelay(isBackgroundCheck ? 0f : 3f);
                }
                else if (response.status == "requires_activation")
                {
                    ShowLicenseWindow();
                    ShowStatusText("Для использования лицензии необходима активация");
                }
                else if (!isBackgroundCheck)
                {
                    HandleLicenseCheckError(response);
                }
            }
            else if (www.result == UnityWebRequest.Result.ConnectionError)
            {
                Debug.LogError("Ошибка соединения при проверке лицензии: " + www.error);
                if (!isBackgroundCheck)
                {
                    ShowLicenseWindow();
                    ShowStatusText("Ошибка соединения с сервером");
                }
            }
            else if (www.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"Ошибка протокола при проверке лицензии: {www.responseCode} - {www.error}");
                Debug.Log($"Сырой ответ от сервера (проверка ошибка протокола): {www.downloadHandler.text}");
                if (!isBackgroundCheck)
                {
                    var response = JsonUtility.FromJson<LicenseResponse>(www.downloadHandler.text);
                    HandleLicenseCheckError(response);
                }
            }
        }
    }

    private IEnumerator PerformLicenseActivation(string key)
    {
        Debug.Log($"Attempting to activate with key: {key} for product: {Application.productName}");
        string url = "https://gostdev.ru:8443/activate_license";

        // --- ИЗМЕНЕНИЕ 3: Формируем запрос с product_name ---
        string currentProductName = Application.productName;
        LicenseRequest requestData = new LicenseRequest {
            key = key,
            device_id = deviceId,
            product_name = currentProductName
        };
        string json = JsonUtility.ToJson(requestData);
        // --- Конец ИЗМЕНЕНИЯ 3 ---

        Debug.Log($"Activation JSON: {json}");

        using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.SetRequestHeader("app-version", Application.version);

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"Сырой ответ от сервера (активация успех): {www.downloadHandler.text}");
                var response = JsonUtility.FromJson<LicenseResponse>(www.downloadHandler.text);
                Debug.Log($"ActivateLicense Result: Status={response.status}, Message={response.message}");
                ShowStatusText("Лицензия успешно активирована");
                PlayerPrefs.SetString("license_key", key);
                PlayerPrefs.SetInt("isLicensed", 1);
                HideLicenseWindowAfterDelay(3f);
            }
            else if (www.result == UnityWebRequest.Result.ConnectionError)
            {
                Debug.LogError("Ошибка соединения при активации лицензии: " + www.error);
                ShowStatusText("Ошибка соединения с сервером при активации");
            }
            else if (www.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"Ошибка протокола при активации лицензии: {www.responseCode} - {www.error}");
                Debug.Log($"Сырой ответ от сервера (активация ошибка протокола): {www.downloadHandler.text}");
                var response = JsonUtility.FromJson<LicenseResponse>(www.downloadHandler.text);
                HandleLicenseActivationError(response, www.responseCode);
            }
        }
    }

    private void HandleLicenseCheckError(LicenseResponse response)
    {
        ShowLicenseWindow();
        if (response == null)
        {
            ShowStatusText("Ошибка при проверке лицензии. Не удалось разобрать ответ сервера.");
            return;
        }

        switch (response.status)
        {
            case "not_found":
                ShowStatusText("Лицензионный ключ не найден");
                break;
            case "inactive":
                ShowStatusText("Лицензия неактивна");
                break;
            case "expired":
                ShowStatusText("Срок действия лицензии истек");
                break;
            case "device_mismatch":
                ShowStatusText("Лицензия активирована на другом устройстве");
                break;
            case "version_mismatch":
                ShowStatusText($"Лицензия предназначена для версии приложения {response.intended_version}"); // Возвращаем показ версии
                break;
            case "product_mismatch": // Потенциально новый статус ответа сервера
                ShowStatusText($"Лицензия предназначена для другого продукта: {response.intended_product_name}");
                break;
            case "error":
                ShowStatusText($"Ошибка проверки лицензии: {response.message}");
                break;
            default:
                ShowStatusText($"Ошибка при проверке лицензии: {response.message} (Статус: {response.status})");
                break;
        }
    }

    private void HandleLicenseActivationError(LicenseResponse response, long responseCode)
    {
        ShowLicenseWindow();
        if (response == null)
        {
            ShowStatusText($"Ошибка активации: {responseCode}. Не удалось разобрать ответ сервера.");
            return;
        }

        switch (response.status)
        {
            case "not_found":
                ShowStatusText("Лицензионный ключ не найден");
                break;
            case "inactive":
                ShowStatusText("Лицензия неактивна");
                break;
            case "expired":
                ShowStatusText("Срок действия лицензии истек");
                break;
            case "device_mismatch":
                ShowStatusText("Лицензия уже активирована на другом устройстве");
                break;
            case "device_already_has_license":
                ShowStatusText("На данном устройстве уже активирована другая лицензия для этого продукта");
                break;
            case "version_mismatch":
                ShowStatusText($"Лицензия предназначена для версии приложения {response.intended_version}"); // Возвращаем показ версии
                break;
            case "product_mismatch": // Потенциально новый статус ответа сервера
                ShowStatusText($"Лицензия предназначена для другого продукта: {response.intended_product_name}");
                break;
            case "error":
                ShowStatusText($"Ошибка активации лицензии: {response.message}");
                break;
            default:
                ShowStatusText($"Ошибка активации: {response.message} (Статус: {response.status}, Код: {responseCode})");
                break;
        }
    }

    private void ShowStatusText(string message)
    {
        statusText.text = message;
        statusText.gameObject.SetActive(true);
        StartCoroutine(HideStatusTextAfterDelay(3f));
    }

    private IEnumerator HideStatusTextAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        statusText.gameObject.SetActive(false);
    }

    private void ShowLicenseWindow()
    {
        licenseWindow.SetActive(true);
        blockingOverlayContainer.SetActive(true);
    }

    private void HideLicenseWindowAfterDelay(float delay)
    {
        StartCoroutine(HideLicenseWindowCoroutine(delay));
    }

    private IEnumerator HideLicenseWindowCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        licenseWindow.SetActive(false);
        blockingOverlayContainer.SetActive(false);
    }

    private void RemoveLicenseAfterDelay()
    {
        PlayerPrefs.DeleteKey("isLicensed");
        PlayerPrefs.DeleteKey("license_key");
    }


    private void OnKeyInputChange(string text)
    {
        if (activateButton != null && activateButton.GetComponentInChildren<Text>() != null)
        {
            if (string.IsNullOrEmpty(text))
            {
                if (sizeChangeCoroutine != null)
                {
                    StopCoroutine(sizeChangeCoroutine);
                }
                sizeChangeCoroutine = StartCoroutine(ShrinkButtonCoroutine());

                activateButton.GetComponentInChildren<Text>().text = "Приобрести лицензию";
            }
            else
            {
                if (sizeChangeCoroutine != null)
                {
                    StopCoroutine(sizeChangeCoroutine);
                }
                sizeChangeCoroutine = StartCoroutine(ExpandButtonCoroutine());
                exitButton.gameObject.SetActive(false);

                activateButton.GetComponentInChildren<Text>().text = "Активировать";
            }
        }
        else
        {
            Debug.LogError("Ошибка: Кнопка активации или текст на ней не найдены!");
        }
    }

    private IEnumerator ExpandButtonCoroutine()
    {
        RectTransform buttonRect = activateButton.GetComponent<RectTransform>();
        while (buttonRect.sizeDelta.x < 700)
        {
            buttonRect.sizeDelta = Vector2.Lerp(buttonRect.sizeDelta, new Vector2(700, buttonRect.sizeDelta.y), Time.deltaTime * 5);
            yield return null;
        }
        buttonRect.sizeDelta = new Vector2(700, buttonRect.sizeDelta.y);
    }

    private IEnumerator ShrinkButtonCoroutine()
    {
        RectTransform buttonRect = activateButton.GetComponent<RectTransform>();
        while (buttonRect.sizeDelta.x > 342)
        {
            buttonRect.sizeDelta = Vector2.Lerp(buttonRect.sizeDelta, new Vector2(340, buttonRect.sizeDelta.y), Time.deltaTime * 5);
            yield return null;
        }
        buttonRect.sizeDelta = new Vector2(340, buttonRect.sizeDelta.y);
        exitButton.gameObject.SetActive(true);
    }

    private void ShowTrialPeriodDays()
    {
        // Проверяем, была ли лицензия когда-либо активирована (даже если сейчас нет интернета)
        // Если isLicensed = 1, значит, активация была успешной.
        if (PlayerPrefs.GetInt("isLicensed", 0) == 1)
        {
            string startDateStr = PlayerPrefs.GetString("startDate", "");
            if (string.IsNullOrEmpty(startDateStr))
            {
                // Записываем дату только если лицензия была когда-то успешно активирована
                // и это первый запуск после этого, когда мы начинаем отсчет.
                // Эта логика больше для оффлайн триала ПОСЛЕ успешной онлайн активации, если она была.
                // Для чистого триала БЕЗ активации нужна другая логика.
                // У вас сейчас "isLicensed" ставится только при УСПЕШНОЙ активации.
                // Если это просто триал без ключа, то "isLicensed" будет 0.

                // Если текущая логика ShowTrialPeriodDays вызывается при isLicensed = 1 (т.е. ключ был активирован),
                // то это не совсем "триал", а скорее "льготный период работы оффлайн".
                // Давайте немного переосмыслим или уточним, когда вызывается ShowTrialPeriodDays.
                // Судя по Start(), он вызывается, если есть сохраненный ключ, но нет интернета.

                PlayerPrefs.SetString("startDate", System.DateTime.UtcNow.ToString());
                startDateStr = System.DateTime.UtcNow.ToString();
            }
            System.DateTime startDate = System.DateTime.Parse(startDateStr);
            int daysRemaining = 60 - (int)(System.DateTime.UtcNow - startDate).TotalDays;

            if (daysRemaining <= 0)
            {
                PlayerPrefs.DeleteKey("isLicensed"); // Лицензия больше не считается валидной оффлайн
                PlayerPrefs.DeleteKey("license_key"); // Ключ тоже можно удалить или оставить для повторной онлайн-проверки
                ShowStatusText("Пробный период (оффлайн) закончился. Для работы нужен интернет для проверки лицензии");
                ShowLicenseWindow(); // Показать окно, так как лицензия больше не действительна
            }
            else
            {
                ShowStatusText($"Работа в оффлайн-режиме. Осталось {daysRemaining} дней");
                // Не скрываем окно лицензии, если пользователь должен видеть статус
            }
        }
        else
        {
            // Если isLicensed = 0, значит, либо нет ключа, либо он не был активирован.
            // Логика триала без ключа здесь не прописана.
            // Если подразумевается, что ShowTrialPeriodDays вызывается только для неактивированных ключей,
            // то текущая проверка if (PlayerPrefs.GetInt("isLicensed", 0) == 1) не даст этому коду выполниться.
            // Нужно уточнить сценарий для ShowTrialPeriodDays.
        }
    }
}