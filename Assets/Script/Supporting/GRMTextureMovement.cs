using UnityEngine;

public class GRMTextureMovement : MonoBehaviour
{
    public Material materialToAnimate;

    [SerializeField] private float _scrollSpeed = 0.1f; // Приватная переменная с полем для инспектора
    private bool isMoving = false;

    public float ScrollSpeed // Публичное свойство
    {
        get { return _scrollSpeed; }
        private set // Сделаем setter приватным, чтобы скорость менялась только через StartMovement
        {
            _scrollSpeed = value;
        }
    }

    public void StartMovement(float speed)
    {
        if (!isMoving)
        {
            ScrollSpeed = speed; // Устанавливаем скорость через свойство
            isMoving = true;
            StartCoroutine(ScrollTexture());
        }
        else
        {
            Debug.Log("Texture movement is already running.");
        }
    }

    public void StopMovement()
    {
        if (isMoving)
        {
            isMoving = false;
            StopCoroutine(ScrollTexture());
        }
        else {}
    }

    private System.Collections.IEnumerator ScrollTexture()
    {
        while (isMoving)
        {
            // Получаем текущее смещение текстуры
            Vector2 offset = materialToAnimate.mainTextureOffset;

            // Увеличиваем смещение по оси X
            offset.x += Time.deltaTime * ScrollSpeed;

            // Зацикливаем текстуру
            if (offset.x > 1f)
            {
                offset.x -= 1f;
            }

            // Применяем новое смещение к материалу
            materialToAnimate.mainTextureOffset = offset;

            yield return null; // Ждем следующий кадр
        }
    }
}