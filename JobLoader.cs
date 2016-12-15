using KdSoft.Config;
using KdSoft.Utils;
using Common.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using Quartz;

namespace KdSoft.Quartz.Jobs
{
    public class JobLoader: IDisposable
    {
        IScheduler scheduler;
        FileChangeDetector fileDetector;
        ILog log;
        HashSet<string> assemblyDirectories;

        public const string ConfigAssemblyNamePattern = "*.jobconfig";

        public JobLoader(string baseDirectory, TimeSpan settleTime) {
            assemblyDirectories = new HashSet<string>();

            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

            var notifyFilters = NotifyFilters.LastWrite | NotifyFilters.FileName;
            fileDetector = new FileChangeDetector(baseDirectory, "*.zip", false, notifyFilters, settleTime);
            fileDetector.FileChanged += fileDetector_FileChanged;

            ExtractExistingZipFiles();
            // we consider all sub-directories as job assembly locations
            var directories = Directory.EnumerateDirectories(baseDirectory);
            foreach (var assemblyDir in directories) {
                assemblyDirectories.Add(assemblyDir);
            }
        }

        Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args) {
            var requestingPath = args.RequestingAssembly == null ? null : args.RequestingAssembly.CodeBase;
            var baseName = args.Name.Split(',')[0];
            var matchingFiles = FindFiles(requestingPath, baseName + ".dll");
            foreach (var match in matchingFiles) {
                try {
                    return Assembly.LoadFrom(match);
                }
                catch { }
            }

            return null;
        }

        IList<string> FindFiles(string priorityDir, string filePattern) {
            List<string> result = new List<string>();

            if (!string.IsNullOrEmpty(priorityDir)) {
                priorityDir = Path.GetFullPath(priorityDir);
                result.AddRange(Directory.EnumerateFiles(priorityDir, filePattern, SearchOption.TopDirectoryOnly));
            }

            foreach (var dir in assemblyDirectories) {
                var fullDir = Path.GetFullPath(dir);
                if (string.Equals(fullDir, priorityDir, StringComparison.OrdinalIgnoreCase))
                    continue;
                result.AddRange(Directory.EnumerateFiles(dir, filePattern, SearchOption.TopDirectoryOnly));
            }
            return result;
        }

        void fileDetector_FileChanged(object sender, FileSystemEventArgs e) {
            string extractDir = ExtractZipFile(e.FullPath);
            if (extractDir == null)
                return;

            // for AssemblyResolve event to work
            assemblyDirectories.Add(extractDir);

            try {
                var cfg = LoadJobConfigurator(extractDir);
                if (cfg != null)
                    cfg.Configure(scheduler);
            }
            catch (Exception ex) {
                log.Error(m => m("Failure loading job." + Environment.NewLine + ex.Message));
                return;
            }
        }

        // all configuration assembly files must have unique names
        IConfigurator<IScheduler> LoadJobConfigurator(string jobDir) {
            IConfigurator<IScheduler> result = null;

            // first, check if we have the config assembly in source code form
            var dirInfo = new DirectoryInfo(jobDir);
            var configSource = dirInfo.EnumerateFiles(ConfigAssemblyNamePattern + ".cs").FirstOrDefault();
            if (configSource != null) {
                string assemblyFile = Path.ChangeExtension(configSource.Name, ".dll");
                result = ConfigUtil.GetConfigurator<IScheduler>(jobDir, configSource.Name, assemblyFile, false);
            }

            // if no suitable source was found or compiled, check if we can load the actual assembly
            if (result == null) {
                var assemblyFileInfo = dirInfo.EnumerateFiles(ConfigAssemblyNamePattern + ".dll").FirstOrDefault();
                if (assemblyFileInfo != null) {
                    var configAssembly = Assembly.LoadFrom(assemblyFileInfo.FullName);
                    result = ConfigUtil.GetConfigurator<IScheduler>(configAssembly);
                }
            }

            return result;
        }

        string ExtractZipFile(string zipFilePath) {
            string extractDir = Path.Combine(fileDetector.BaseDirectory, Path.GetFileNameWithoutExtension(zipFilePath));
            if (Directory.Exists(extractDir))
                return null;
            try {
                ZipFile.ExtractToDirectory(zipFilePath, extractDir);
            }
            catch (Exception ex) {
                log.Error(m => m("Failure unzipping." + Environment.NewLine + ex.Message));
                return null;
            }
            return extractDir;
        }

        void ExtractExistingZipFiles() {
            var zipFiles = Directory.EnumerateFiles(fileDetector.BaseDirectory, "*.zip", SearchOption.TopDirectoryOnly);
            foreach (var zipFile in zipFiles)
                ExtractZipFile(zipFile);
        }

        /// <summary>
        /// Starts the job loader using the scheduler passed in.
        /// </summary>
        /// <remarks>We do not pass the scheduler to the constructor because we want a chance
        /// to resolve the existing job assemblies before the scheduler tries to load them.</remarks>
        /// <param name="scheduler"><see cref="IScheduler" /> instance to be used by this job loader.</param>
        public void Start(IScheduler scheduler, ILog log) {
            if (fileDetector == null)
                throw new ObjectDisposedException("JobLoader");

            this.scheduler = scheduler;
            this.log = log;

            foreach (var assemblyDir in assemblyDirectories) {
                try {
                    var cfg = LoadJobConfigurator(assemblyDir);
                    if (cfg != null)
                        cfg.Configure(scheduler);
                }
                catch (Exception ex) {
                    log.Error(m => m("Failure configuring job." + Environment.NewLine + ex.Message));
                }
            }

            fileDetector.Start(true);
        }

        public void Dispose() {
            AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;

            var fd = fileDetector;
            if (fd != null) {
                fd.Dispose();
                fd = null;
            }
            GC.SuppressFinalize(this);
        }
    }
}
