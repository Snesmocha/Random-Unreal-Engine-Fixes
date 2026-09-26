// Copyright Epic Games, Inc. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Tools.DotNETCommon;

namespace UnrealBuildTool
{
	/// <summary>
	/// Generate a clang compile_commands file for a target.
	///
	/// Reimplemented to address two sources of bloat in the stock UBT implementation:
	///   1. Definitions/include paths are de-duplicated per module (stock UBT can emit the
	///      same -D/-I flag multiple times when it's inherited via more than one dependency path).
	///   2. All per-module flags (defines, includes, force-includes, std version) are written once
	///      to a response file (@Module.rsp) instead of being repeated verbatim in every single
	///      file's "command" entry. Each per-file entry becomes just:
	///          clang++.exe @Module.rsp file.cpp
	///      which both clang and libclang's CompilationDatabase parser expand transparently.
	/// </summary>
	[ToolMode("GenerateClangDatabase", ToolModeOptions.XmlConfig | ToolModeOptions.BuildPlatforms | ToolModeOptions.SingleInstance | ToolModeOptions.StartPrefetchingEngine | ToolModeOptions.ShowExecutionTime)]
	class GenerateClangDatabase : ToolMode
	{
		/// <summary>
		/// Set of filters for files to include in the database. Relative to the root directory, or to the project file.
		/// </summary>
		[CommandLine("-Filter=")]
		List<string> FilterRules = new List<string>();

		/// <summary>
		/// If set, falls back to the old behaviour of inlining every flag into each file's command
		/// instead of factoring them into a per-module response file. Useful if your tooling doesn't
		/// support @response-file expansion.
		/// </summary>
		[CommandLine("-NoResponseFile")]
		bool bNoResponseFile = false;

		/// <summary>
		/// Execute the command
		/// </summary>
		/// <param name="Arguments">Command line arguments</param>
		/// <returns>Exit code</returns>
		public override int Execute(CommandLineArguments Arguments)
		{
			Arguments.ApplyTo(this);

			// Create the build configuration object, and read the settings
			BuildConfiguration BuildConfiguration = new BuildConfiguration();
			XmlConfig.ApplyTo(BuildConfiguration);
			Arguments.ApplyTo(BuildConfiguration);

			// Parse the filter argument
			FileFilter FileFilter = null;
			if (FilterRules.Count > 0)
			{
				FileFilter = new FileFilter(FileFilterType.Exclude);
				foreach (string FilterRule in FilterRules)
				{
					FileFilter.AddRules(FilterRule.Split(';'));
				}
			}

			// Parse all the target descriptors
			List<TargetDescriptor> TargetDescriptors = TargetDescriptor.ParseCommandLine(Arguments, BuildConfiguration.bUsePrecompiled, BuildConfiguration.bSkipRulesCompile);

			// Directory where per-module response files are written. Kept under Intermediate so it's
			// disposable and doesn't pollute source control.
			DirectoryReference ResponseFileDirectory = DirectoryReference.Combine(UnrealBuildTool.EngineDirectory, "Intermediate", "ClangDatabase");
			if (!bNoResponseFile)
			{
				DirectoryReference.CreateDirectory(ResponseFileDirectory);
			}

			// Cache of response files we've already written, keyed by content hash, so two modules
			// that happen to end up with identical flag sets (rare but possible for tiny modules)
			// share a single .rsp instead of writing duplicate files.
			Dictionary<string, FileReference> ContentHashToResponseFile = new Dictionary<string, FileReference>();

			// Generate the compile DB for each target
			using (ISourceFileWorkingSet WorkingSet = new EmptySourceFileWorkingSet())
			{
				// Find the compile commands for each file in the target
				Dictionary<FileReference, string> FileToCommand = new Dictionary<FileReference, string>();
				foreach (TargetDescriptor TargetDescriptor in TargetDescriptors)
				{
					// Disable PCHs and unity builds for the target
					TargetDescriptor.AdditionalArguments = TargetDescriptor.AdditionalArguments.Append(new string[] { "-NoPCH", "-DisableUnity" });

					// Create a makefile for the target
					UEBuildTarget Target = UEBuildTarget.Create(TargetDescriptor, BuildConfiguration.bSkipRulesCompile, BuildConfiguration.bUsePrecompiled);

					// Find the location of the compiler
					VCEnvironment Environment = VCEnvironment.Create(WindowsCompiler.Clang, Target.Platform, Target.Rules.WindowsPlatform.Architecture, null, Target.Rules.WindowsPlatform.WindowsSdkVersion, null);
					FileReference ClangPath = FileReference.Combine(Environment.CompilerDir, "bin", "clang++.exe");

					// Convince each module to output its generated code include path
					foreach (UEBuildBinary Binary in Target.Binaries)
					{
						foreach (UEBuildModuleCPP Module in Binary.Modules.OfType<UEBuildModuleCPP>())
						{
							Module.bAddGeneratedCodeIncludePath = true;
						}
					}

					// Create all the binaries and modules
					CppCompileEnvironment GlobalCompileEnvironment = Target.CreateCompileEnvironmentForProjectFiles();
					foreach (UEBuildBinary Binary in Target.Binaries)
					{
						CppCompileEnvironment BinaryCompileEnvironment = Binary.CreateBinaryCompileEnvironment(GlobalCompileEnvironment);
						foreach (UEBuildModuleCPP Module in Binary.Modules.OfType<UEBuildModuleCPP>())
						{
							if (Module.Rules.bUsePrecompiled)
							{
								continue;
							}

							UEBuildModuleCPP.InputFileCollection InputFileCollection = Module.FindInputFiles(Target.Platform, new Dictionary<DirectoryItem, FileItem[]>());

							List<FileItem> InputFiles = new List<FileItem>();
							InputFiles.AddRange(InputFileCollection.CPPFiles);
							InputFiles.AddRange(InputFileCollection.CCFiles);

							if (InputFiles.Count == 0)
							{
								continue;
							}

							CppCompileEnvironment ModuleCompileEnvironment = Module.CreateModuleCompileEnvironment(Target.Rules, BinaryCompileEnvironment);

							// Build the list of module-level flags, de-duplicated. Order is preserved
							// (first occurrence wins) so include-order-sensitive headers still behave.
							List<string> ModuleFlags = new List<string>();

							if (ModuleCompileEnvironment.CppStandard >= CppStandardVersion.Cpp17)
							{
								ModuleFlags.Add("-std=c++17");
							}
							else if (ModuleCompileEnvironment.CppStandard >= CppStandardVersion.Cpp14)
							{
								ModuleFlags.Add("-std=c++14");
							}

							foreach (FileItem ForceIncludeFile in ModuleCompileEnvironment.ForceIncludeFiles.DistinctBy(f => f.FullName))
							{
								ModuleFlags.Add(String.Format("-include \"{0}\"", ForceIncludeFile.FullName));
							}
							foreach (string Definition in ModuleCompileEnvironment.Definitions.Distinct())
							{
								ModuleFlags.Add(String.Format("-D\"{0}\"", Definition));
							}
							foreach (DirectoryReference IncludePath in ModuleCompileEnvironment.UserIncludePaths.Distinct())
							{
								ModuleFlags.Add(String.Format("-I\"{0}\"", IncludePath));
							}
							foreach (DirectoryReference IncludePath in ModuleCompileEnvironment.SystemIncludePaths.Distinct())
							{
								ModuleFlags.Add(String.Format("-I\"{0}\"", IncludePath));
							}

							string ModulePrefix;
							if (bNoResponseFile)
							{
								// Old behaviour: inline everything into every file's command.
								ModulePrefix = String.Format("\"{0}\" {1}", ClangPath.FullName, String.Join(" ", ModuleFlags));
							}
							else
							{
								string ResponseFileContent = String.Join("\n", ModuleFlags);
								string ContentHash = ComputeHash(ResponseFileContent);

								FileReference ResponseFile;
								if (!ContentHashToResponseFile.TryGetValue(ContentHash, out ResponseFile))
								{
									string SafeModuleName = SanitizeFileName(Module.Name);
									ResponseFile = FileReference.Combine(ResponseFileDirectory, String.Format("{0}.{1}.rsp", SafeModuleName, ContentHash.Substring(0, 8)));
									FileReference.WriteAllText(ResponseFile, ResponseFileContent);
									ContentHashToResponseFile[ContentHash] = ResponseFile;
								}

								ModulePrefix = String.Format("\"{0}\" @\"{1}\"", ClangPath.FullName, ResponseFile.FullName);
							}

							foreach (FileItem InputFile in InputFiles)
							{
								if (FileFilter == null || FileFilter.Matches(InputFile.Location.MakeRelativeTo(UnrealBuildTool.RootDirectory)))
								{
									FileToCommand[InputFile.Location] = String.Format("{0} \"{1}\"", ModulePrefix, InputFile.FullName);
								}
							}
						}
					}
				}

				// Write the compile database
				FileReference DatabaseFile = FileReference.Combine(UnrealBuildTool.RootDirectory, "compile_commands.json");
				using (JsonWriter Writer = new JsonWriter(DatabaseFile))
				{
					Writer.WriteArrayStart();
					foreach (KeyValuePair<FileReference, string> FileCommandPair in FileToCommand.OrderBy(x => x.Key.FullName))
					{
						Writer.WriteObjectStart();
						Writer.WriteValue("file", FileCommandPair.Key.FullName);
						Writer.WriteValue("command", FileCommandPair.Value);
						Writer.WriteValue("directory", UnrealBuildTool.EngineSourceDirectory.ToString());
						Writer.WriteObjectEnd();
					}
					Writer.WriteArrayEnd();
				}

				Log.TraceInformation("Wrote compile_commands.json ({0} entries, {1} response files in {2})",
					FileToCommand.Count, ContentHashToResponseFile.Count, ResponseFileDirectory);
			}

			return 0;
		}

		/// <summary>
		/// Computes a short, stable hash of a response file's content so identical module flag sets
		/// across different modules can share one .rsp file.
		/// </summary>
		static string ComputeHash(string Content)
		{
			using (SHA256 Sha256 = SHA256.Create())
			{
				byte[] Bytes = Sha256.ComputeHash(Encoding.UTF8.GetBytes(Content));
				StringBuilder Builder = new StringBuilder(Bytes.Length * 2);
				foreach (byte B in Bytes)
				{
					Builder.Append(B.ToString("x2"));
				}
				return Builder.ToString();
			}
		}

		/// <summary>
		/// Strips characters that aren't safe in a Windows filename from a module name.
		/// </summary>
		static string SanitizeFileName(string Name)
		{
			StringBuilder Builder = new StringBuilder(Name.Length);
			foreach (char C in Name)
			{
				Builder.Append(Char.IsLetterOrDigit(C) || C == '_' || C == '-' ? C : '_');
			}
			return Builder.ToString();
		}
	}

	/// <summary>
	/// Small LINQ helper since UBT's target C#/.NET version at this point in the engine's history
	/// predates System.Linq.DistinctBy.
	/// </summary>
	static class ClangDatabaseLinqExtensions
	{
		public static IEnumerable<T> DistinctBy<T, TKey>(this IEnumerable<T> Source, Func<T, TKey> KeySelector)
		{
			HashSet<TKey> Seen = new HashSet<TKey>();
			foreach (T Item in Source)
			{
				if (Seen.Add(KeySelector(Item)))
				{
					yield return Item;
				}
			}
		}
	}
}