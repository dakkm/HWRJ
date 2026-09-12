using System.Collections.ObjectModel;

namespace PreProcess.Wpf.ViewModels
{
    // Presentation state only; no business task or backend contract model.
    public sealed class NavigationItemViewModel
    {
        public string Title { get; }
        public string Description { get; }
        public ObservableCollection<NavigationItemViewModel> Children { get; }
            = new ObservableCollection<NavigationItemViewModel>();

        public NavigationItemViewModel(string title, string description)
        {
            Title = title;
            Description = description;
        }
    }
}
