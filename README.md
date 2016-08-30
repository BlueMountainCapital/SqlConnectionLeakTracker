# SqlConnectionLeakTracker


# How to use:
- Add a reference to SqlConnectionLeakTracker from your project.
- Call SqlConnectionLeakTracker.SqlConnectionWrapper.PrintInstatiatedConnections or PrintOpenConnections at a point in your code where you would be interested in how many currently undisposed SqlConnection objects are around.
- Run SqlConnectionLeakTracker on the assemblies that are instantiating/opening SqlConnections that are not being closed/disposed. E.g. `SqlConnectionLeakTracker TestApp.exe` This rewrites the IL in TestApp.exe to replace e.g. all calls to `new SqlConnection()` or `IDisposable.Dispose()` to go through a wrapper function that keeps track of SqlConnection objects.


# Development:
- There are some barebones tests--running TestApp.csproj will run runtest.bat which will run SqlConnectionLeakTracker on TestApp.exe, verify the executable, and test for some hard-coded expected output.
- This could probably be expanded to look for any kind of undisposed IDisposable
