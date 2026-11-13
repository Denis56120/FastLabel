namespace FastLabel.Client.Bases;

public abstract class TranslatableComponent : LayoutComponentBase, IDisposable
{
    [Inject] protected TranslationService Translation { get; set; } = default!;

    private bool _isDisposed;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        Translation.OnChange += OnTranslationChanged;
    }

    private async void OnTranslationChanged()
    {
        if (_isDisposed) return;

        try
        {
            await OnLanguageChangedAsync();

            if (!_isDisposed)
            {
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (Exception)
        {
        }
    }

    protected virtual Task OnLanguageChangedAsync()
    {
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _isDisposed = true;
        Translation.OnChange -= OnTranslationChanged;
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}