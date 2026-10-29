namespace EdilManager.Desktop.Services;

public interface INavigationService
{
    void Push(object page);
    void Pop();
}

public interface IReloadable
{
    Task ReloadAsync();
}
