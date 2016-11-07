#region Using Directives

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using JetBrains.Annotations;

#endregion

namespace IInspectable.ProjectExplorer.Extension {

    class Hierarchy {

        static readonly Logger Logger = Logger.Create<Hierarchy>();
        readonly SolutionService _solutionService;
        readonly IVsHierarchy _vsHierarchy;
        readonly HierarchyId _itemId;

        public Hierarchy(SolutionService solutionService, IVsHierarchy vsHierarchy, HierarchyId itemId) {

            if(solutionService == null) {
                throw new ArgumentNullException(nameof(solutionService));
            }

            if(vsHierarchy == null) {
                throw new ArgumentNullException(nameof(vsHierarchy));
            }

            _solutionService = solutionService;
            _vsHierarchy = vsHierarchy;
            _itemId = itemId;
        }

        public HierarchyId ItemId {
            get { return _itemId; }
        }

        IVsSolution VsSolution1 {
            get { return _solutionService.VsSolution1; }
        }

        IVsSolution4 VsSolution4 {
            get { return _solutionService.VsSolution4; }
        }

        public string Name {
            get { return GetProperty<string>(__VSHPROPID.VSHPROPID_Name); }
        }

        public string CanonicalName {
            get {
                string cn;
                LogFailed(_vsHierarchy.GetCanonicalName(ItemId, out cn));
                return cn;
            }
        }

        public Guid ProjectGuid {
           get { return _solutionService.GetProjectGuid(_vsHierarchy); }
        }

        #region  Structural Properties

        public HierarchyId ParentItemId {
            get { return GetProperty<int>(__VSHPROPID.VSHPROPID_Parent, HierarchyId.Nil); }
        }

        public Hierarchy Parent {
            get { return new Hierarchy(_solutionService, _vsHierarchy, ParentItemId); }
        }

        public HierarchyId ParentHierarchyItemId {
            get {
                object id = GetProperty<object>(__VSHPROPID.VSHPROPID_ParentHierarchyItemid);

                if(id is int) {
                    return (uint) (int) id;
                }
                if(id is uint) {
                    return (uint) id;
                }

                return HierarchyId.Nil;
            }
        }

        public Hierarchy ParentHierarchy {
            get {
                var parentHierarchy = GetProperty<IVsHierarchy>(__VSHPROPID.VSHPROPID_ParentHierarchy);

                return parentHierarchy == null ? null
                    : new Hierarchy(_solutionService, parentHierarchy, VSConstants.VSITEMID_ROOT);
            }
        }

        public bool IsNestedHierachy {
            get { return ItemId.IsRoot && !ParentHierarchyItemId.IsNil; }
        }

        public HierarchyId FirstChildItemId {
            get { return GetProperty<int>(__VSHPROPID.VSHPROPID_FirstChild, HierarchyId.Nil); }
        }

        [CanBeNull]
        public Hierarchy FirstChild {
            get { return WithId(FirstChildItemId); }
        }
        
        public HierarchyId FirstVisibleChildItemId {
            get { return GetProperty<int>(__VSHPROPID.VSHPROPID_FirstVisibleChild, HierarchyId.Nil); }
        }

        [CanBeNull]
        public Hierarchy FirstVisibleChild {
            get { return WithId(FirstVisibleChildItemId); }
        }

        public HierarchyId NextSiblingItemId {
            get { return GetProperty<int>(__VSHPROPID.VSHPROPID_NextSibling, HierarchyId.Nil); }
        }

        [CanBeNull]
        public Hierarchy NextSibling {
            get { return WithId(NextSiblingItemId); }
        }

        public HierarchyId NextVisibleSiblingItemId {
            get { return GetProperty<int>(__VSHPROPID.VSHPROPID_NextVisibleSibling, HierarchyId.Nil); }
        }

        [CanBeNull]
        public Hierarchy NextVisibleSibling {
            get { return WithId(NextVisibleSiblingItemId); }
        }

        public IEnumerable<Hierarchy> Children() {

            if(GetProperty<bool>(__VSHPROPID.VSHPROPID_HasEnumerationSideEffects)) {
                yield break;
            }
            
            var firstChild = FirstChild;
            if (firstChild == null) {
                yield break;
            }
            yield return firstChild.GetNestedHierarchy() ?? firstChild;

            var sibling = firstChild.NextSibling;
            while (sibling != null) {
                yield return sibling.GetNestedHierarchy() ?? sibling;
                sibling = sibling.NextSibling;
            }
        }

        public IEnumerable<Hierarchy> VisibleChildren() {

            if (GetProperty<bool>(__VSHPROPID.VSHPROPID_HasEnumerationSideEffects)) {
                yield break;
            }

            var firstChild = FirstVisibleChild;
            if (firstChild == null) {
                yield break;
            }
            yield return firstChild.GetNestedHierarchy() ?? firstChild;

            var sibling = firstChild.NextVisibleSibling;
            while (sibling != null) {
                yield return sibling.GetNestedHierarchy() ?? sibling;
                sibling = sibling.NextSibling;
            }
        }

        public IEnumerable<Hierarchy> DescendantsAndSelf() {
            yield return GetNestedHierarchy() ?? this;
            foreach (var descendant in Descendants()) {
                yield return descendant;
            }
        }

        public IEnumerable<Hierarchy> Descendants() {
            foreach(var child in Children()) {
                foreach(var descendant in child.DescendantsAndSelf()) {
                    yield return descendant;
                }
            }
        }

        public IEnumerable<Hierarchy> VisibleDescendantsAndSelf() {            
            yield return GetNestedHierarchy()??this;
            foreach (var descendant in VisibleDescendants()) {
                yield return descendant;
            }
        }

        public IEnumerable<Hierarchy> VisibleDescendants() {
            foreach (var child in VisibleChildren()) {
                foreach (var descendant in child.DescendantsAndSelf()) {
                    yield return descendant;
                }
            }
        }

        [CanBeNull]
        Hierarchy WithId(HierarchyId hierarchyId) {
            if(hierarchyId.IsNil) {
                return null;
            }
            return new Hierarchy(_solutionService, _vsHierarchy, hierarchyId);
        }

        #endregion

        public string SaveName {
            get { return GetProperty<String>(__VSHPROPID.VSHPROPID_SaveName);
            }
        }

        public string FullPath {
            get {
                var fullPath= GetMkDocument() ?? GetCanonicalName();

                if (!Path.IsPathRooted(fullPath)) {
                    return null;
                }
                return fullPath;
            }
        }

        [CanBeNull]
        public Hierarchy GetNestedHierarchy() {
            var nestedHierarchyGuid = typeof(IVsHierarchy).GUID;
            uint nestedItemId;
            IntPtr nestedHiearchyValue;
            LogFailed(_vsHierarchy.GetNestedHierarchy(ItemId, ref nestedHierarchyGuid, out nestedHiearchyValue, out nestedItemId));

            if(nestedHiearchyValue == IntPtr.Zero) {
                return null;
            }

            var nestedHierarchy = Marshal.GetObjectForIUnknown(nestedHiearchyValue) as IVsHierarchy;
            Marshal.Release(nestedHiearchyValue);
            if(nestedHierarchy == null) {
                return null;
            }

            return new Hierarchy(_solutionService, nestedHierarchy, nestedItemId);
        }

        [CanBeNull]
        public string GetMkDocument() {
            // ReSharper disable once SuspiciousTypeConversion.Global
            var ao = _vsHierarchy as IVsProject;
            string doc=null;
            ao?.GetMkDocument(ItemId, out doc);
            return doc;
        }

        [CanBeNull]
        public string GetCanonicalName() {
            string cn= null;
            LogFailed(_vsHierarchy?.GetCanonicalName(ItemId, out cn)?? VSConstants.S_OK);
            return cn;
        }

        public override string ToString() {
            return Name;
        }

        public string DumpAll() {
            return DumpCore(h => h.Children());
        }

        public string DumpVisible() {
            return DumpCore(h => h.VisibleChildren());
        }

        string DumpCore(Func<Hierarchy, IEnumerable<Hierarchy>> childSelector, int maxLevel = Int32.MaxValue) {
            StringBuilder sb = new StringBuilder();
            Dump(this, 0, sb, childSelector, maxLevel);

            return sb.ToString();
        }

        static void Dump(Hierarchy hier, int level, StringBuilder sb, Func<Hierarchy, IEnumerable<Hierarchy>> childSelector, int maxLevel= Int32.MaxValue) {

            if (level > maxLevel) {
                return;
            }

            var indent = new String(' ', level*2);
            sb.AppendLine($"{indent}{hier.Name}: '{hier.FullPath}'");
            foreach(var child in childSelector(hier)) {
                Dump(child, level+1, sb, childSelector, maxLevel);
            }
        }


        public ImageMoniker GetImageMoniker() {
            return _solutionService.GetImageMonikerForHierarchyItem(_vsHierarchy);
        }

        public int UnloadProject() {

            Guid projectGuid = GetProjectGuid();
            return LogFailed(VsSolution4.UnloadProject(ref projectGuid, (uint) _VSProjectUnloadStatus.UNLOADSTATUS_UnloadedByUser));
        }

        public int ReloadProject() {

            Guid projectGuid = GetProjectGuid();
            return LogFailed(VsSolution4.ReloadProject(ref projectGuid));
        }

        public int CloseProject() {

            return LogFailed(VsSolution1.CloseSolutionElement(
                grfCloseOpts: (uint) __VSSLNCLOSEOPTIONS.SLNCLOSEOPT_SLNSAVEOPT_MASK | (uint) __VSSLNCLOSEOPTIONS.SLNCLOSEOPT_DeleteProject,
                pHier: _vsHierarchy,
                docCookie: 0));
        }

        public Guid GetProjectGuid() {
            Guid projGuid;
            LogFailed(VsSolution1.GetGuidOfProject(_vsHierarchy, out projGuid));
            return projGuid;
        }

        public string GetUniqueNameOfProject() {
            string uniqueName;
            LogFailed(VsSolution1.GetUniqueNameOfProject(_vsHierarchy, out uniqueName));
            return uniqueName?.ToLower();
        }

        public bool IsProjectUnloaded() {

            object status;
            var hr = _vsHierarchy.GetProperty(_itemId, (int) __VSHPROPID5.VSHPROPID_ProjectUnloadStatus, out status);

            return ErrorHandler.Succeeded(hr);
        }
       
        public uint AdviseHierarchyEvents(IVsHierarchyEvents eventSink) {
            uint cookie;
            LogFailed(_vsHierarchy.AdviseHierarchyEvents(eventSink, out cookie));
            return cookie;
        }

        public int UnadviseHierarchyEvents(uint cookie) {
            return LogFailed(_vsHierarchy.UnadviseHierarchyEvents(cookie));
        }
        
        protected T GetProperty<T>(__VSHPROPID propId, T defaultValue=default(T)) {
            var value= GetPropertyCore((int)propId);
            if(value == null) {
                return defaultValue;
            }
            return (T)value;
        }
        
        protected object GetPropertyCore(int propId) {

            if(propId == (int) __VSHPROPID.VSHPROPID_NIL) {
                return null;
            }

            object propValue;

            LogFailed(_vsHierarchy.GetProperty(ItemId, propId, out propValue));

            return propValue;
        }

        int LogFailed(int hr, [CallerMemberName] string callerMemberName = null) {
            if (ErrorHandler.Failed(hr)) {
                var ex = Marshal.GetExceptionForHR(hr);
                Logger.Warn($"{callerMemberName} failed with code 0x{hr:X}: '{ex.Message}'");
            }
            return hr;
        }
    }
}