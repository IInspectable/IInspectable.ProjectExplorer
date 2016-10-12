#region Using Directives

using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using IInspectable.Utilities.IO;
using IInspectable.Utilities.Logging;
using JetBrains.Annotations;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Shell.Interop;


#endregion

namespace IInspectable.ProjectExplorer.Extension {

    class SolutionService: IVsSolutionEvents, IVsSolutionEvents4, IDisposable {

        readonly IVsSolution  _vsSolution1;
        readonly IVsSolution2 _vsSolution2;
        readonly IVsSolution4 _vsSolution4;
        readonly IVsImageService2 _vsImageService2;
        static readonly Logger Logger = Logger.Create<SolutionService>();

        uint _solutionEvents1Cookie;
        uint _solutionEvents4Cookie;

        public SolutionService() {

            _vsSolution1 = ProjectExplorerPackage.GetGlobalService<SVsSolution, IVsSolution>();
            _vsSolution2 = ProjectExplorerPackage.GetGlobalService<SVsSolution, IVsSolution2>();
            _vsSolution4 = ProjectExplorerPackage.GetGlobalService<SVsSolution, IVsSolution4>();

            _vsImageService2 = ProjectExplorerPackage.GetGlobalService<SVsImageService, IVsImageService2>();
            
            _vsSolution1.AdviseSolutionEvents(this, out _solutionEvents1Cookie);
            _vsSolution1.AdviseSolutionEvents(this, out _solutionEvents4Cookie);           
        }

        public void Dispose() {
            if(_solutionEvents1Cookie != 0) {
                _vsSolution1.UnadviseSolutionEvents(_solutionEvents1Cookie);
                _solutionEvents1Cookie = 0;
            }

            if (_solutionEvents4Cookie != 0) {
                _vsSolution1.UnadviseSolutionEvents(_solutionEvents4Cookie);
                _solutionEvents4Cookie = 0;
            }
        }

        public IVsSolution VsSolution1 {
            get { return _vsSolution1; }
        }

        public IVsSolution2 VsSolution2 {
            get { return _vsSolution2; }
        }

        public IVsSolution4 VsSolution4 {
            get { return _vsSolution4; }
        }

        public ImageMoniker GetImageMonikerForFile(string filename) {
            return _vsImageService2.GetImageMonikerForFile(filename);
        }

        public ImageMoniker GetImageMonikerForHierarchyItem(IVsHierarchy hierarchy) {
            return _vsImageService2.GetImageMonikerForHierarchyItem(hierarchy, (uint)VSConstants.VSITEMID.Root, (int)__VSHIERARCHYIMAGEASPECT.HIA_Icon);
        }

        public Task<List<ProjectFile>> GetProjectFilesAsync(string path, CancellationToken cancellationToken) {
            
            var task = Task.Run(() => {

                var patterns = new[] { "*.csproj"};//, "*.vbproj", "*.vcxproj", "*.jsproj", "*.fsproj" };

                var projectFiles = new List<ProjectFile>();

                if (String.IsNullOrWhiteSpace(path)) {
                    Logger.Warn($"{nameof(GetProjectFilesAsync)}: path is null or empty");
                    return projectFiles;
                }

                foreach(var pattern in patterns) {
                    foreach (var file in Directory.EnumerateFiles(path, pattern, SearchOption.AllDirectories)) {

                        cancellationToken.ThrowIfCancellationRequested();

                        ProjectFile projectFile  =null;

                        try {
                            projectFile = ProjectFile.FromFile(file);
                            
                        } catch (Exception ex) {
                            Logger.Error(ex, $"Fehler beim Laden der Projektdatei '{file}'");
                        }

                        if (projectFile != null) {                            
                            projectFiles.Add(projectFile);
                        }
                    }
                }
                
                return projectFiles;
            }, cancellationToken);

            return task;
        }

        public List<ProjectViewModel> BindToHierarchy(List<ProjectFile> projectFiles) {
            var projectFileViewModels = new List<ProjectViewModel>();

            var projectHierarchyById = GetProjectHierarchyByUniqueName();

            foreach (var projectFile in projectFiles) {

                var uniqueNameOfProject = PathHelper.GetRelativePath(GetSolutionDirectory(), projectFile.Path)
                                                    .ToLowerInvariant();

                var vm = new ProjectViewModel(projectFile, uniqueNameOfProject);

                Hierarchy pHierarchy;
                if(projectHierarchyById.TryGetValue(uniqueNameOfProject, out pHierarchy)) {
                    vm.Bind(pHierarchy);
                }

                projectFileViewModels.Add(vm);
            }

            return projectFileViewModels;
        }

        public int OpenProject(string path) {

            Guid empty = Guid.Empty;
            Guid projId=Guid.Empty;
            IntPtr ppProj;

            return LogFailed(_vsSolution1.CreateProject(
                rguidProjectType: ref empty,
                lpszMoniker     : path,
                lpszLocation    : null,
                lpszName        : null,
                grfCreateFlags  : (uint) __VSCREATEPROJFLAGS.CPF_OPENFILE,
                iidProject      : ref projId,
                ppProject       : out ppProj));
        }

        public Guid GetProjectGuid(IVsHierarchy pHierarchy) {
            Guid projGuid;
            LogFailed(_vsSolution1.GetGuidOfProject(pHierarchy, out projGuid));              
            return projGuid;
        }

        public bool IsSolutionLoaded() {
            return GetSolutionDirectory() != null;
        }

        [CanBeNull]
        public string GetSolutionDirectory() {

            string solutionDirectory;
            string solutionFile;
            string userOptsFile;

            _vsSolution1.GetSolutionInfo(out solutionDirectory, out solutionFile, out userOptsFile);

            return solutionDirectory;
        }

        [CanBeNull]
        public string GetSolutionFile() {

            string solutionDirectory;
            string solutionFile;
            string userOptsFile;

            _vsSolution1.GetSolutionInfo(out solutionDirectory, out solutionFile, out userOptsFile);

            return solutionFile;
        }

        public Hierarchy GetHierarchyByUniqueNameOfProject(string uniqueName) {
            IVsHierarchy result;
            if (Failed(_vsSolution1.GetProjectOfUniqueName(uniqueName, out result))) {
                return null;
            }

            return new Hierarchy(this, result);
        }
        
        Dictionary<string, Hierarchy> GetProjectHierarchyByUniqueName() {

            var result = new Dictionary<string, Hierarchy>();

            Guid ignored = Guid.Empty;
            IEnumHierarchies hierEnum;
            var flags = __VSENUMPROJFLAGS.EPF_LOADEDINSOLUTION | __VSENUMPROJFLAGS.EPF_UNLOADEDINSOLUTION;
            if (Failed(_vsSolution1.GetProjectEnum((uint)flags, ref ignored, out hierEnum))) {
                return result;
            }

            IVsHierarchy[] hier = new IVsHierarchy[1];
            uint fetched;
            while ((hierEnum.Next((uint)hier.Length, hier, out fetched) == VSConstants.S_OK) && (fetched == hier.Length)) {

                var hierarchy = new Hierarchy(this, hier[0]);

                result[hierarchy.GetUniqueNameOfProject()] =hierarchy;
            }

            return result;
        }

        #region IVsSolutionEvents

        public event EventHandler<ProjectEventArgs> AfterOpenProject;
        int IVsSolutionEvents.OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded) {

            var realHierarchy = new Hierarchy(this, pHierarchy);
            AfterOpenProject?.Invoke(this, new ProjectEventArgs(realHierarchy));

            return VSConstants.S_OK;
        }

        int IVsSolutionEvents.OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel) {
            return VSConstants.S_OK;
        }

        public event EventHandler<ProjectEventArgs> BeforeRemoveProject;
        int IVsSolutionEvents.OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved) {

            if(fRemoved == 1) {
                var realHierarchy = new Hierarchy(this, pHierarchy);
                BeforeRemoveProject?.Invoke(this, new ProjectEventArgs(realHierarchy));
            }

            return VSConstants.S_OK;
        }

        public event EventHandler<ProjectEventArgs> AfterLoadProject;
        int IVsSolutionEvents.OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy) {

            var realHierarchy = new Hierarchy(this, pStubHierarchy);
            var stubHierarchy = new Hierarchy(this, pRealHierarchy);
            AfterLoadProject?.Invoke(this, new ProjectEventArgs(realHierarchy, stubHierarchy));

            return VSConstants.S_OK;
        }

        int IVsSolutionEvents.OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel) {
            return VSConstants.S_OK;
        }

        public event EventHandler<ProjectEventArgs> BeforeUnloadProject;
        int IVsSolutionEvents.OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy) {
            var realHierarchy = new Hierarchy(this, pStubHierarchy);
            var stubHierarchy = new Hierarchy(this, pRealHierarchy);
            BeforeUnloadProject?.Invoke(this, new ProjectEventArgs(realHierarchy, stubHierarchy));
            return VSConstants.S_OK;
        }


        public event EventHandler AfterOpenSolution;
        int IVsSolutionEvents.OnAfterOpenSolution(object pUnkReserved, int fNewSolution) {
            AfterOpenSolution?.Invoke(this, EventArgs.Empty);
            return VSConstants.S_OK;
        }

        int IVsSolutionEvents.OnQueryCloseSolution(object pUnkReserved, ref int pfCancel) {
            return VSConstants.S_OK;
        }

        int IVsSolutionEvents.OnBeforeCloseSolution(object pUnkReserved) {
            return VSConstants.S_OK;
        }

        public event EventHandler AfterCloseSolution;
        int IVsSolutionEvents.OnAfterCloseSolution(object pUnkReserved) {
            AfterCloseSolution?.Invoke(this, EventArgs.Empty);
            return VSConstants.S_OK;
        }

        #endregion

        #region IVsSolutionEvents4

        int IVsSolutionEvents4.OnAfterRenameProject(IVsHierarchy pHierarchy) {
            return VSConstants.S_OK;
        }

        int IVsSolutionEvents4.OnQueryChangeProjectParent(IVsHierarchy pHierarchy, IVsHierarchy pNewParentHier, ref int pfCancel) {
            return VSConstants.S_OK;
        }

        int IVsSolutionEvents4.OnAfterChangeProjectParent(IVsHierarchy pHierarchy) {
            return VSConstants.S_OK;
        }

        int IVsSolutionEvents4.OnAfterAsynchOpenProject(IVsHierarchy pHierarchy, int fAdded) {
            return VSConstants.S_OK;
        }

        #endregion    

        bool Failed(int hr, [CallerMemberName] string callerMemberName = null) {
            // ReSharper disable once ExplicitCallerInfoArgument Ist hier gew�nscht
            return ErrorHandler.Failed(LogFailed(hr, callerMemberName));
        }

        int LogFailed(int hr, [CallerMemberName] string callerMemberName = null) {
            if(ErrorHandler.Failed(hr)) {
                var ex=Marshal.GetExceptionForHR(hr);
                Logger.Error($"{callerMemberName} failed with code 0x{hr:X}: '{ex.Message}'");
            }
            return hr;
        }
    }
}