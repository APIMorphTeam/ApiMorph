namespace MockDemo.Services;

public sealed class WidgetService
{
    public async Task ActivateAsync(string widgetId)
    {
        var client = new LegacyWidgetClient();
        await client.ActivateAsync(widgetId);
    }
}

public sealed class LegacyWidgetClient
{
    public Task ActivateAsync(string widgetId) => Task.CompletedTask;
}
