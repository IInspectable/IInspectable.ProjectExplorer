using System;
using System.Windows;

namespace IInspectable.ProjectExplorer.Extension {

    sealed class SettingsCommand : Command {

        readonly ProjectExplorerViewModel _viewModel;

        public SettingsCommand(ProjectExplorerViewModel viewModel)
            : base(PackageIds.SettingsCommandId) {

            if (viewModel == null) {
                throw new ArgumentNullException(nameof(viewModel));
            }

            _viewModel = viewModel;
        }

        public override void UpdateState() {
            Enabled = _viewModel.IsSolutionLoaded;
        }

        public override void Execute(object parameter) {
            MessageBox.Show("Coming soon ;-)");
        }
    }

}