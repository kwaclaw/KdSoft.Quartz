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
  /// <summary>
  /// Loads Quartz job assembly. Compiles C# configuration script if necessary.
  /// </summary>
  public class JobLoader: IDisposable
  {
    IScheduler scheduler;
    ILog log;
    FileChangeDetector jobDetector;
    FileChangeDetector codeUpdateDetector;

    readonly HashSet<string> assemblyDirectories;

    /// <summary>Assembly name pattern to use for job configuration scripts.</summary>
    public const string ConfigAssemblyNamePattern = "*.jobconfig";

    /// <param name="baseDirectory">Base directory for Quartz jobs.</param>
    /// <param name="settleTime">Settle time for detecting file changes. That is, a file change is reported
    /// only after the given time span has elapsed without further file changes being detected.</param>
    public JobLoader(string baseDirectory, TimeSpan settleTime) {
      assemblyDirectories = new HashSet<string>();

      AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

      var jobNotifyFilters = NotifyFilters.LastWrite | NotifyFilters.FileName;
      jobDetector = new FileChangeDetector(baseDirectory, "*.zip", false, jobNotifyFilters, settleTime);
      jobDetector.FileChanged += JobDetector_FileChanged;

      var codeNotifyFilters = NotifyFilters.LastWrite | NotifyFilters.FileName;
      codeUpdateDetector = new FileChangeDetector(baseDirectory, "*.cs", true, codeNotifyFilters, settleTime);
      codeUpdateDetector.FileChanged += CodeUpdateDetector_FileChanged;

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
        catch { /* ignore error */ }
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

    void LoadJobDirectory(string jobDir) {
      // for AssemblyResolve event to work
      assemblyDirectories.Add(jobDir);

      try {
        var cfg = LoadJobConfigurator(jobDir);
        if (cfg != null)
          cfg.Configure(scheduler);
      }
      catch (Exception ex) {
        log.Error(m => m("Failure loading job." + Environment.NewLine + ex.Message));
        return;
      }
    }

    void JobDetector_FileChanged(object sender, FileSystemEventArgs e) {
      string extractDir = ExtractZipFile(e.FullPath);
      if (extractDir == null)
        return;
      LoadJobDirectory(extractDir);
    }

    void CodeUpdateDetector_FileChanged(object sender, FileSystemEventArgs e) {
      string jobDir = Path.GetDirectoryName(e.FullPath);
      assemblyDirectories.Add(jobDir);
      LoadJobDirectory(jobDir);
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
      string extractDir = Path.Combine(jobDetector.BaseDirectory, Path.GetFileNameWithoutExtension(zipFilePath));
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
      var zipFiles = Directory.EnumerateFiles(jobDetector.BaseDirectory, "*.zip", SearchOption.TopDirectoryOnly);
      foreach (var zipFile in zipFiles)
        ExtractZipFile(zipFile);
    }

    /// <summary>
    /// Starts the job loader using the scheduler passed in.
    /// </summary>
    /// <remarks>We do not pass the scheduler to the constructor because we want a chance
    /// to resolve the existing job assemblies before the scheduler tries to load them.</remarks>
    /// <param name="scheduler"><see cref="IScheduler" /> instance to be used by this job loader.</param>
    /// <param name="log">Logger to use.</param>
    public void Start(IScheduler scheduler, ILog log) {
      if (jobDetector == null || codeUpdateDetector == null)
        throw new ObjectDisposedException("JobLoader");

      this.scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
      this.log = log ?? throw new ArgumentNullException(nameof(log));

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

      jobDetector.Start(true);
      codeUpdateDetector.Start(false);
    }

    /// <inheritdoc/>
    public void Dispose() {
      AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;

      var jd = jobDetector;
      if (jd != null) {
        jd.Dispose();
        jobDetector = null;
      }

      var cud = codeUpdateDetector;
      if (cud != null) {
        cud.Dispose();
        codeUpdateDetector = null;
      }
      GC.SuppressFinalize(this);
    }
  }
}
