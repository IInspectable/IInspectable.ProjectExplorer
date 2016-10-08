using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace IInspectable.ProjectExplorer.Extension {

    public class ViewModelBase: INotifyPropertyChanged {

        public event PropertyChangedEventHandler PropertyChanged;

        protected void NotifyPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected void NotifyAllPropertiesChanged() {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        }
    }
}