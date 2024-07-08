# GW Launcher Development
## GitHub Actions Build Pipeline
GW Launcher build pipeline can be found [here](../.github/workflows/build.yml).

Steps:
- Calculate version using [GitVersion](https://gitversion.net/) based on the Git tag
- Build GW Launcher using the `SelfContained` [publish profile](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/visual-studio-publish-profiles)
- Conditionally upload GW Launcher executable as a build [artifact](https://docs.github.com/en/actions/using-workflows/storing-workflow-data-as-artifacts) if building off the default branch

## Local
### Compilation
GW Launcher can be built via [Visual Studio](https://visualstudio.microsoft.com/) or with [MSBuild](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-command-line-reference). It cannot be built via `dotnet` (see [here](https://aka.ms/msbuild/MSB4803)).

#### Example MSBuild command
`MSBuild.exe "GW Launcher\GW Launcher.csproj" /target:"Restore;Publish" /m /nodeReuse:false /property:"PublishProfile=SelfContained;Version=1.0.0.0"`

#### Calculating version
GitVersion can be installed and invoked via `dotnet`.
```
dotnet tool restore
dotnet dotnet-gitversion
```
[Versioning behaviour](https://gitversion.net/docs/reference/configuration) is defined in the [GitVersion.yml](../GitVersion.yml) file.