namespace FastLabel.Core;

public interface IFastLabelPlugin
{
    void Launch(string[] args);
    void Stop();
}