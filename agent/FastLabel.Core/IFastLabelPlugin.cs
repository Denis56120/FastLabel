namespace FastLabel.Core;

public interface IFastLabelPlugin
{
    void Launch(string baseUrl);
    void Stop();
}