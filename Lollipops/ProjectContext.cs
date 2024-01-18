namespace Lollipops;

using NuGet.Common;
using NuGet.Packaging;
using NuGet.ProjectManagement;
using System.Xml.Linq;

internal class ProjectContext : INuGetProjectContext {
    public PackageExtractionContext? PackageExtractionContext { get; set; } = null;

    public ISourceControlManagerProvider? SourceControlManagerProvider => null;

    public ExecutionContext? ExecutionContext => null;

    public XDocument? OriginalPackagesConfig { get; set; } = null;

    public NuGetActionType ActionType { get; set; }

    public Guid OperationId { get; set; }

    public void Log(MessageLevel level, string message, params object[] args) {
    }

    public void Log(ILogMessage message) {
    }

    public void ReportError(string message) {
    }

    public void ReportError(ILogMessage message) {
    }

    public FileConflictAction ResolveFileConflict(string message) {
        return FileConflictAction.Ignore;
    }
}

