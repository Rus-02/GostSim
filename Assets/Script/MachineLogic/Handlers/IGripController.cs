using System;

public interface IGripController
{
    void ClampUpper(Action onComplete = null);
    void UnclampUpper(Action onComplete = null);
    
    void ClampLower(Action onComplete = null);
    void UnclampLower(Action onComplete = null);

    bool IsUpperClamped { get; }
    bool IsLowerClamped { get; }
    bool IsAnimating { get; }
}
