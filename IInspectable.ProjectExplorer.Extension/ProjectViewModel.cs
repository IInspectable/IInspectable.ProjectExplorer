#region Using Directives

using System;
using System.Diagnostics;
using JetBrains.Annotations;

#endregion

namespace IInspectable.ProjectExplorer.Extension {

    class ProjectViewModel: ViewModelBase {

        [NotNull]
        readonly ProjectFile _projectFile;

        [CanBeNull]
        Hierarchy _hierarchy;

        [CanBeNull]
        ProjectExplorerViewModel _parent;
        readonly string _uniqueNameOfProject;
        
        public ProjectViewModel(ProjectFile projectFile, string uniqueNameOfProject) {

            if (projectFile == null) {
                throw new ArgumentNullException(nameof(projectFile));
            }

            if (uniqueNameOfProject == null) {
                throw new ArgumentNullException(nameof(uniqueNameOfProject));
            }

            _projectFile         = projectFile;
            _uniqueNameOfProject = uniqueNameOfProject;
        }

        [NotNull]
        public string UniqueNameOfProject {
            get { return _uniqueNameOfProject; }
        }

        [CanBeNull]
        public ProjectExplorerViewModel Parent {
            get { return _parent; }
        }

        public string Name {
            get { return _projectFile.Name; }
        }

        public string Directory {
            get { return System.IO.Path.GetDirectoryName(_projectFile.Path); }
        }

        public string Path {
            get { return _projectFile.Path; }
        }
       
        public ProjectStatus Status {
            get {

                if(_hierarchy == null || _parent==null) {
                    return ProjectStatus.Closed;
                }

                return _hierarchy.IsProjectUnloaded() ? ProjectStatus.Unloaded: ProjectStatus.Loaded;                
            }
        }

        // TODO Confirmation/Fehlerbehandlung
        public void Add() {
            _parent?.SolutionService.OpenProject(_projectFile.Path);
        }

        // TODO Confirmation/Fehlerbehandlung
        public void Remove() {      
            _hierarchy?.CloseProject();
        }

        // TODO Confirmation/Fehlerbehandlung
        public void Load() {
            _hierarchy?.LoadProject();            
        }

        // TODO Confirmation/Fehlerbehandlung
        public void Unload() {
            _hierarchy?.UnloadProject();           
        }

        // TODO Confirmation/Fehlerbehandlung
        public void DefaultAction() {
            switch (Status) {
                case ProjectStatus.Closed:
                    Add();
                    break;
                case ProjectStatus.Unloaded:
                    Load();
                    break;
                case ProjectStatus.Loaded:
                    Unload();
                    break;
            }
        }

        public void OpenFolderInFileExplorer() {

            // TODO Error Handling
            string args = $"/e, /select, \"{Path}\"";

            ProcessStartInfo info = new ProcessStartInfo {
                FileName = "explorer",
                Arguments = args
            };
            Process.Start(info);
        }

        public void Bind([CanBeNull] Hierarchy hierarchy) {
            _hierarchy = hierarchy;
            NotifyAllPropertiesChanged();
            _parent?.UpdateCommands();
        }

        public void SetParent([NotNull] ProjectExplorerViewModel parent) {
            if(parent == null) {
                throw new ArgumentNullException(nameof(parent));
            }
            _parent = parent; 
        }

        public void Dispose() {
            _parent    = null;
            _hierarchy = null;
        }
    }
}