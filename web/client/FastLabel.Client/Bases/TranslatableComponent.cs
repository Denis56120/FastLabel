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

    private void OnTranslationChanged()
    {
        InvokeAsync(StateHasChanged);
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