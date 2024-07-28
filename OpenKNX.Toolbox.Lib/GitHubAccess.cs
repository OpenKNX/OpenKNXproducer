using System.Text.Json;
using Octokit;
using OpenKNX.Toolbox.Lib.Data;

namespace OpenKNX.Toolbox.Lib
{
    public static class GitHubAccess
    {
        private const string OPEN_KNX_ORG = "OpenKNX";
        private const string OPEN_KNX_REPO_DEFAULT_START = "OAM-";
        private const string OPEN_KNX_DATA_FILE_NAME = "OpenKNX.Toolbox.DataCache.json";

        private static List<string> repositoryWhitelist = new List<string>()
        {
            "SOM-UP",
            "GW-REG1-Dali",
            "SEN-UP1-8xTH",
            "BEM-GardenControl"
        };

        /// <summary>
        /// Checks if a "OpenKnxData.json" file is present and not older than one day, otherwise download OpenKNX data from GitHub.
        /// </summary>
        /// <param name="dataDirectory">The directory to store the data file.</param>
        /// <returns>Returns a "OpenKnxData" object in case of success.</returns>
        public static async Task<OpenKnxData?> GetOpenKnxData(string dataDirectory)
        {
            var dataFilePath = Path.Combine(dataDirectory, OPEN_KNX_DATA_FILE_NAME);
            if (File.Exists(dataFilePath))
            {
                if (File.GetLastWriteTimeUtc(dataFilePath) > DateTime.UtcNow.AddDays(-1))
                {
                    using (var fileStream = new FileStream(dataFilePath, System.IO.FileMode.Open))
                        return await JsonSerializer.DeserializeAsync<OpenKnxData>(fileStream);
                }
            }

            var openKnxData = new OpenKnxData();
            openKnxData.Projects = await GetOpenKnxProjects();
            openKnxData.Projects.Sort();

            using (var fileStream = new FileStream(dataFilePath, System.IO.FileMode.Create))
                await JsonSerializer.SerializeAsync(fileStream, openKnxData);

            return openKnxData;
        }

        /// <summary>
        /// Load OpenKNX projects data from GitHub.
        /// </summary>
        /// <returns>The OpenKNX projects list.</returns>
        private static async Task<List<OpenKnxProject>> GetOpenKnxProjects()
        {
            var openKnxProjects = new List<OpenKnxProject>();
            var client = new GitHubClient(new ProductHeaderValue(OPEN_KNX_ORG));

            var repositories = await client.Repository.GetAllForOrg(OPEN_KNX_ORG);
            foreach (var repository in repositories)
            {
                if (!repository.Name.StartsWith(OPEN_KNX_REPO_DEFAULT_START) &&
                    !repositoryWhitelist.Contains(repository.Name))
                    continue;

                var openKnxProject = new OpenKnxProject(repository.Id, repository.Name);

                var releases = await client.Repository.Release.GetAll(repository.Id);
                foreach (var release in releases)
                {
                    if (string.IsNullOrEmpty(release.Name))
                        continue;

                    var openKnxRelease = new OpenKnxRelease(release.Id, release.Name);

                    foreach (var asset in release.Assets)
                    {
                        if (!asset.Name.ToLower().EndsWith(".zip"))
                            continue;

                        openKnxRelease.Files.Add(new OpenKnxReleaseFile(asset.Id, asset.Name, asset.BrowserDownloadUrl));
                    }

                    if (openKnxRelease.Files.Count > 0)
                    {
                        openKnxRelease.Files.Sort();
                        openKnxProject.Releases.Add(openKnxRelease);
                    }
                }

                if (openKnxProject.Releases.Count > 0)
                {
                    openKnxProject.Releases.Sort((a, b) => b.CompareTo(a));
                    openKnxProjects.Add(openKnxProject);
                }
            }

            return openKnxProjects;
        }

        /// <summary>
        /// Downloads a release ZIP file from GitHub.
        /// </summary>
        /// <param name="releaseFile">The "OpenKnxReleaseFile" object containing the download URL.</param>
        /// <returns>The file path where the downloaded ZIP is stored.</returns>
        /// <exception cref="FileNotFoundException"></exception>
        public static async Task<string> DownloadReleaseFile(OpenKnxReleaseFile releaseFile)
        {
            using (var client = new HttpClient())
            {
                var response = await client.GetAsync(releaseFile.DownloadUrl);
                if (response.IsSuccessStatusCode)
                {
                    var contentStream = await response.Content.ReadAsStreamAsync();
                    var targetPath = Path.Combine(TempData.Instance.GetTempPath(), releaseFile.Name);
                    var fileStream = new FileStream(targetPath, System.IO.FileMode.Create);
                    await contentStream.CopyToAsync(fileStream).ContinueWith(
                        (copyTask) =>
                        {
                            fileStream.Close();
                        });

                    return targetPath;
                }
                else
                {
                    throw new FileNotFoundException();
                }
            }
        }
    }
}