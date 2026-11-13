namespace FastLabel.Client.Services;

public class ToastService
{
    public event Action<string, ToastType>? OnShow;

    public void ShowError(string messageKey) => OnShow?.Invoke(messageKey, ToastType.Error);
    public void ShowSuccess(string messageKey) => OnShow?.Invoke(messageKey, ToastType.Success);
}

public enum ToastType 
{ 
    Success, 
    Error 
}