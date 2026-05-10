
## Project Context

This is a Unity C# project.

## Working Rules

- Prefer minimal changes.
- Preserve existing naming conventions and public APIs.
- Do not install dependencies unless explicitly requested.
- Do not use network access unless explicitly requested.
- Do not run Unity Editor automatically.
- Do not modify files outside the repository unless explicitly requested.

## Validation Rules

- Prefer static code review and local C# reasoning first.
- If validation requires Unity Editor, external SDKs, network access, or elevated permissions, stop and report the exact command the user should run manually.
- After changes, summarize modified files, expected behavior, and manual Unity test steps.

## Shell Command Rules

- Do not run broad recursive scans over the whole project.
- Do not use Get-ChildItem -Recurse on Assets, Library, Packages, Temp, Logs, Obj, or UserSettings unless explicitly requested.
- Prefer targeted searches with rg in specific source directories.
- Do not run multiple shell commands in parallel when inspecting the project.
- Do not run dotnet build on Unity-generated .sln or .csproj files unless explicitly requested.
- For Unity validation, provide manual Unity Editor test steps instead of launching Unity or building through dotnet.
- Do not use dotnet build as the primary validation method for this Unity project.
- Prefer static code review.
- If compilation validation is needed, ask the user to run Unity Editor compilation or Unity Test Runner manually.