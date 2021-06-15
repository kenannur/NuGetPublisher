using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Gtk;
using Microsoft.Build.Construction;
using MonoDevelop.Components.Commands;
using MonoDevelop.Ide;
using MonoDevelop.Projects;

namespace NuGetPublisher
{
    /// <summary>
    /// After project build
    /// mono "/Applications/Visual Studio.app/Contents/Resources/lib/monodevelop/bin/vstool.exe" setup pack NuGetPublisher.dll
    /// command must run to create "NuGetPublisher_1.0.mpack" package.
    /// </summary>
    public class PublishHandler : CommandHandler
    {
        private const int VersionMaxValue = 10;
        private const string Configuration = "Release";
        private const string GitHubSource = "<GITHUB_SOURCE>";
        private const string GitHubApiKey = "<GITHUB_API_KEY>";
        private StringBuilder stringBuilder;

        protected override void Run()
        {
            stringBuilder = new StringBuilder();
            using (var selectedProject = IdeApp.ProjectOperations.CurrentSelectedProject)
            {
                if (selectedProject == null)
                {
                    return;
                }
                UpdateProjectMetadata(selectedProject, out string newPackageVersion);
                Pack(selectedProject);
                Thread.Sleep(1000);
                Push(selectedProject, newPackageVersion);
                ShowDialog();
            }
        }

        private void UpdateProjectMetadata(Project project, out string newPackageVersion)
        {
            var csprojPath = project.FileName.FullPath.ToString();
            var projectRootElement = ProjectRootElement.Open(csprojPath);
            var packageVersionElement = projectRootElement.Properties.FirstOrDefault(x => x.Name == "PackageVersion");
            newPackageVersion = packageVersionElement == null ? "1.0.0" : GetNextPackageVersion(packageVersionElement.Value);
            projectRootElement.AddProperty("PackageVersion", newPackageVersion);
            projectRootElement.Save();
        }

        private void Pack(Project project)
        {
            StartNewProcess($"pack {project.FileName.FullPath} --configuration {Configuration}");
        }

        private void Push(Project project, string newPackageVersion)
        {
            var packageName = $"{project.Name}.{newPackageVersion}.nupkg";
            var packagePath = Path.Combine(project.BaseDirectory, "bin", Configuration, packageName);
            StartNewProcess($"nuget push {packagePath} --source {GitHubSource} --api-key {GitHubApiKey}");
        }

        private void StartNewProcess(string arguments)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                }
            };
            process.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
            {
                var data = e.Data;
                stringBuilder.AppendLine(data);
                Console.WriteLine(data);
            });
            process.Start();
            process.BeginOutputReadLine();
            process.WaitForExit();
            process.Close();
        }

        private string GetNextPackageVersion(string packageVersion)
        {
            var majorMinorPatch = packageVersion.Split('.');
            var currentMajor = int.Parse(majorMinorPatch[0]);
            var currentMinor = int.Parse(majorMinorPatch[1]);
            var currentPatch = int.Parse(majorMinorPatch[2]);

            if (currentPatch + 1 < VersionMaxValue)
            {
                return $"{currentMajor}.{currentMinor}.{currentPatch + 1}";
            }
            else if (currentMinor + 1 < VersionMaxValue)
            {
                return $"{currentMajor}.{currentMinor + 1}.0";
            }
            else
            {
                return $"{currentMajor + 1}.0.0";
            }
        }

        private void ShowDialog()
        {
            var dialog = new Dialog
            {
                Title = "Publish Result",
                DefaultWidth = 500,
                DefaultHeight = 200,
                Resizable = true
            };
            dialog.SetPosition(WindowPosition.Center);
            dialog.VBox.Add(new Label(stringBuilder.ToString()));
            dialog.ShowAll();
        }
    }
}
