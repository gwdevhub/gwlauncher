# GW Launcher Development
## GitHub Actions Build Pipeline
GW Launcher build pipeline can be found [here](../.github/workflows/build.yml).

Steps:
- Calculate version using [GitVersion](https://gitversion.net/) based on the Git tag
- Build GW Launcher twice, using the `SelfContained` and `FrameworkDependent` [publish profiles](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/visual-studio-publish-profiles), and rename each to `GW_Launcher.exe` and `GW_Launcher_Framework_Dependent.exe`
- Conditionally upload GW Launcher executable as a build [artifact](https://docs.github.com/en/actions/using-workflows/storing-workflow-data-as-artifacts) if building off the default branch

## Local
### Compilation
GW Launcher can be built via [Visual Studio](https://visualstudio.microsoft.com/) or with [MSBuild](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-command-line-reference). It cannot be built via `dotnet` (see [here](https://aka.ms/msbuild/MSB4803)).

#### Building both executables
`publish.bat` builds both, in the same way as the workflow, into `Publish\`:
- `GW_Launcher.exe`: self-contained, runs without installing .NET.
- `GW_Launcher_Framework_Dependent.exe`: needs the .NET 10 Desktop Runtime installed.

Each profile writes to the same folder, so the script renames the first output before building the second.

#### Example MSBuild command
`MSBuild.exe "GW Launcher\GW Launcher.csproj" /target:"Restore;Publish" /m /nodeReuse:false /property:"PublishProfile=SelfContained"`

#### Calculating version
GitVersion can be installed and invoked via `dotnet`.
```
dotnet tool restore
dotnet dotnet-gitversion
```
[Versioning behaviour](https://gitversion.net/docs/reference/configuration) is defined in the [GitVersion.yml](../GitVersion.yml) file.