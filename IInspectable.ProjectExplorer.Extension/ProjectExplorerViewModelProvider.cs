#region Using Directives

using Microsoft.VisualStudio.Shell;
using System.ComponentModel.Composition;

#endregion

namespace IInspectable.ProjectExplorer.Extension {

    [Export]
    class ProjectExplorerViewModelProvider {

        [Import] readonly SolutionService _solutionService;
        [Import] readonly OptionService _optionService;
        [Import] readonly IWaitIndicator _waitIndicator;

        public ProjectExplorerViewModel CreateViewModel(IErrorInfoService errorInfoService, OleMenuCommandService oleMenuCommandService) {

            return new ProjectExplorerViewModel(errorInfoService, _solutionService, _optionService, oleMenuCommandService, _waitIndicator);
        }
    }
}