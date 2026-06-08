using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace GoogleCalendarManagement.ViewModels;

public sealed class ComfyUIFolderItemViewModel : ObservableObject
{
    public int Id { get; }
    public string FolderPath { get; }

    public IAsyncRelayCommand RemoveCommand { get; }

    public ComfyUIFolderItemViewModel(int id, string folderPath, Func<int, Task> onRemove)
    {
        Id = id;
        FolderPath = folderPath;
        RemoveCommand = new AsyncRelayCommand(() => onRemove(id));
    }
}
