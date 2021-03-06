﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;

namespace Rynchodon.PluginLoader
{
	internal class PluginData
	{

		private const string fileName = "Settings.json";
		/// <summary>
		/// Don't load this one!
		/// </summary>
		private static readonly PluginName loadArms = new PluginName("Rynchodon", "Load-ARMS");

		private readonly string _directory;

		/// <summary>
		/// Plugins that are configured to be downloaded.
		/// </summary>
		private readonly Dictionary<PluginName, PluginConfig> _gitHubConfig = new Dictionary<PluginName, PluginConfig>();
		/// <summary>
		/// Plugins that have been downloaded.
		/// </summary>
		private readonly Dictionary<PluginName, Plugin> _downloaded = new Dictionary<PluginName, Plugin>();

		public string PathToGit;

		/// <summary>
		/// Configuration for GitHub plugins.
		/// </summary>
		public ICollection<PluginConfig> GitHubConfig
		{
			get { return _gitHubConfig.Values; }
			set
			{
				_gitHubConfig.Clear();
				foreach (PluginConfig config in value)
					AddConfig(config);
			}
		}

		/// <summary>
		/// Configuration for GitHub plugins that have enabled = true.
		/// </summary>
		/// <returns>GitHub plugins that have enabled = true.</returns>
		public IEnumerable<PluginConfig> EnabledGitHubConfig()
		{
			foreach (PluginConfig config in _gitHubConfig.Values)
				if (config.enabled)
					yield return config;
		}

		public PluginData(string directory)
		{
			this._directory = directory;
		}

		private string GetFilePath()
		{
			return PathExtensions.Combine(_directory, fileName);
		}

		/// <summary>
		/// Add a configuration. Load-ARMS is not permitted.
		/// </summary>
		public void AddConfig(PluginConfig config)
		{
			if (config.name.Equals(loadArms))
			{
				Logger.WriteLine("ERROR: Cannot add " + config.name.repository + ", it is incompatible with " + Loader.SeplShort + ". Adding ARMS instead");
				config.name.repository = "ARMS";
			}
			_gitHubConfig[config.name] = config;
		}

		public void AddDownloaded(Plugin plugin)
		{
			_downloaded[plugin.name] = plugin;
		}

		public bool TryGetDownloaded(PluginName name, out Plugin plugin)
		{
			return _downloaded.TryGetValue(name, out plugin);
		}

		public void Load()
		{
			_gitHubConfig.Clear();
			_downloaded.Clear();

			string filePath = GetFilePath();
			Settings set = default(Settings);

			if (File.Exists(filePath))
			{
				FileInfo fileInfo = new FileInfo(filePath);
				fileInfo.IsReadOnly = false;
				try
				{
					Serialization.ReadJson(filePath, out set);
				}
				catch
				{
					Logger.WriteLine("ERROR: Failed to read settings file");
					throw;
				}
				finally
				{
					fileInfo.IsReadOnly = true;
				}
			}

			if (set.GitHubConfig != null)
			{
				Logger.WriteLine("Loading config");
				foreach (PluginConfig config in set.GitHubConfig)
					_gitHubConfig.Add(config.name, config);
			}
			else
			{
				Logger.WriteLine("Create new config");
				PluginConfig config = new PluginConfig(new PluginName("ShawnTheShadow", Loader.SeplRepo), false);
				_gitHubConfig.Add(config.name, config);
			}

			if (set.Downloaded != null)
			{
				Logger.WriteLine("Loading downloads");
				foreach (Plugin plugin in set.Downloaded)
					if (plugin.MissingFile())
						Logger.WriteLine(plugin.name.fullName + " is missing a file, it must be downloaded again");
					else
						_downloaded.Add(plugin.name, plugin);
			}

			PathToGit = set.PathToGit;

			if (string.IsNullOrWhiteSpace(PathToGit) || !File.Exists(PathToGit))
				PathToGit = SearchForGit();
		}

		private string SearchForGit()
		{
			HashSet<string> searchLocations = new HashSet<string>();

			foreach (string envPath in Environment.GetEnvironmentVariable("PATH").Split(';'))
				if (string.IsNullOrWhiteSpace(envPath))
					Logger.WriteLine("empty string in PATH");
				else
					searchLocations.Add(envPath);

			foreach (string envVar in new string[] { "ProgramFiles", "ProgramFiles(x86)", "ProgramW6432" })
			{
				string path = Environment.GetEnvironmentVariable(envVar);
				if (string.IsNullOrWhiteSpace(path))
					Logger.WriteLine("No environment variable: " + envVar);
				else
				{
					searchLocations.Add(PathExtensions.Combine(path, "Git", "bin"));
					searchLocations.Add(PathExtensions.Combine(path, "Git", "cmd"));
				}
			}

			foreach (string location in searchLocations)
			{
				string gitExe = PathExtensions.Combine(location, "git.exe");
				if (File.Exists(gitExe))
				{
					Logger.WriteLine("git @ " + gitExe);
					return gitExe;
				}
			}
			return null;
		}

		public void Save()
		{
			string filePath = GetFilePath();
			Settings set;
			set.Downloaded = _downloaded.Values.ToArray();
			set.GitHubConfig = _gitHubConfig.Values.ToArray();
			set.PathToGit = PathToGit;
			FileInfo fileInfo = new FileInfo(filePath);
			if (File.Exists(filePath))
				fileInfo.IsReadOnly = false;
			try
			{
				Serialization.WriteJson(filePath, set, true);
			}
			catch
			{
				Logger.WriteLine("ERROR: Failed to write settings file");
				throw;
			}
			finally
			{
				fileInfo.IsReadOnly = true;
			}
		}

		[DataContract]
		private struct Settings
		{
			[DataMember]
			public Plugin[] Downloaded;
			[DataMember]
			public PluginConfig[] GitHubConfig;
			[DataMember]
			public string PathToGit;
		}

	}
}
