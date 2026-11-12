using Microsoft.AspNetCore.Components;

namespace FastLabel.Client.Components;

public abstract class TranslatableComponent : LayoutComponentBase, IDisposable
{
    [Inject] protected TranslationService Translation { get; set; } = default!;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        Translation.OnChange += OnTranslationChanged;
    }

    private async void OnTranslationChanged()
    {
        await OnLanguageChangedAsync();

        await InvokeAsync(StateHasChanged);
    }

    protected virtual Task OnLanguageChangedAsync()
    {
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        Translation.OnChange -= OnTranslationChanged;
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}