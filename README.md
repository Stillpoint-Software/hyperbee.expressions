# Inital Project Setup

## Stucture

### Source

All projects should be in a folder with the same name as the project.

### Tests

All test should be in a project related to their project in the tests folder.

### Solution Files

- .github/worflow files include

## Repo Settings

Update the repository settings

### Pull Request Labels

Add the following labels. Label case is important for workflows.

- feature
  - description: Improvements or additions to documentation
  - color: #0075ca
- critical
  - description: Major Release Issue
  - color: #B60205

### Variables

Add the following variable to the repository so that the github action work correctly

- SOLUTION_NAME = "Hyperbee.---.sln"

### Issue Labels

Default labels should line up with the settings in `issue-branch.yml` and any others that might be useful.

## Dependabot

- Enable and Group PRs
- dependabot.yml should be included with

## Nuget Config

### Project File

The Project file should include the basic nuget information in sections like:

```xml
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
     <ImplicitUsings>enable</ImplicitUsings>
	<IsPackable>true</IsPackable>
	<Authors>Stillpoint Software, Inc.</Authors>
	<PackageReadmeFile>README.md</PackageReadmeFile>
	<PackageTags>{TAGS}</PackageTags>
	<PackageIcon>icon.png</PackageIcon>
	<PackageProjectUrl>https://github.com/Stillpoint-Software/Hyperbee.{PACKAGE}/</PackageProjectUrl>
	<PackageReleaseNotes>https://github.com/Stillpoint-Software/Hyperbee.{PACKAGE}/releases/latest</PackageReleaseNotes>
	<TargetFrameworks>net8.0</TargetFrameworks>
	<PackageLicenseFile>LICENSE</PackageLicenseFile>
	<Copyright>Stillpoint Software, Inc.</Copyright>
	<Title>Hyperbee {PACKAGE}</Title>
	<Description>{PACKAGE INFO}</Description>
	<RepositoryUrl>https://github.com/Stillpoint-Software/Hyperbee.{PACKAGE}</RepositoryUrl>
	<RepositoryType>git</RepositoryType>
  </PropertyGroup>
```

And

```xml
  <ItemGroup>
	<None Include="..\..\assets\icon.png" Pack="true" Visible="false" PackagePath="/" />
	<None Include="..\..\LICENSE">
		<Pack>True</Pack>
		<PackagePath>\</PackagePath>
	</None>
	<None Include="..\..\README.md">
		<Pack>True</Pack>
		<PackagePath>\</PackagePath>
	</None>
	<PackageReference Update="Microsoft.SourceLink.GitHub" Version="8.0.0">
		<PrivateAssets>all</PrivateAssets>
		<IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
	</PackageReference>
	<PackageReference Include="Microsoft.CodeAnalysis.CSharp.Scripting" Version="4.9.2" />
  </ItemGroup>
```

### Directory.Build.props

Make sure the section is correctly set for initial release:

```xml
  <PropertyGroup>
    <MajorVersion>1</MajorVersion>
    <MinorVersion>0</MinorVersion>
    <PatchVersion>0</PatchVersion>
  </PropertyGroup>
```

### Status Area

- Add the following paths for the **develop** and **main** Actions. Replace {project} with the hyperbee project name.

  - Develop: `[![Build status](https://github.com/Stillpoint-Software/Hyperbee.{projectName}/actions/workflows/publish.yml/badge.svg?branch=develop)](https://github.com/Stillpoint-Software/Hyperbee.{projectName}/actions/workflows/publish.yml)`

  - Main: `[![Build status](https://github.com/Stillpoint-Software/Hyperbee.{projectName}/actions/workflows/publish.yml/badge.svg)](https://github.com/Stillpoint-Software/Hyperbee.{projectName}/actions/workflows/publish.yml) `

- Add to bottom of status

`[![Hyperbee.{projectName}](https://github.com/Stillpoint-Software/Hyperbee.{projectName}/blob/main/assets/hyperbee.jpg?raw=true)](https://github.com/Stillpoint-Software/Hyperbee.{projectName})`

# -- SAMPLE TEMPATE README --

# Hyperbee.<Project>

Classes for building awesome software

## Usage

```csharp
# Cool usage of the code!
```

# Status

| Branch    | Action       |
| --------- | ------------ |
| `develop` | {links here} |
| `main`    | {links here} |

# Help

See [Todo](https://github.com/Stillpoint-Software/Hyperbee.Project/blob/main/docs/todo.md)
