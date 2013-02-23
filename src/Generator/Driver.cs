﻿using System.IO;
using Cxxi.Generators;
using Cxxi.Generators.CLI;
using Cxxi.Generators.CSharp;
using Cxxi.Passes;
using Cxxi.Types;
using System;
using System.Collections.Generic;

namespace Cxxi
{
    public class Driver
    {
        public DriverOptions Options { get; private set; }
        public ILibrary Transform { get; private set; }
        public IDiagnosticConsumer Diagnostics { get; private set; }
        public TypeMapDatabase TypeDatabase { get; private set; }
        public Library Library { get; private set; }

        public Driver(DriverOptions options, ILibrary transform)
        {
            Options = options;
            Transform = transform;
            Diagnostics = new TextDiagnosticPrinter();
            TypeDatabase = new TypeMapDatabase();
        }

        public void Setup()
        {
            TypeDatabase.SetupTypeMaps();

            if (Transform != null)
                Transform.Setup(Options);

            ValidateOptions();
        }

        private void ValidateOptions()
        {
            if (string.IsNullOrWhiteSpace(Options.LibraryName))
                throw new InvalidDataException();

            if (Options.OutputDir == null)
                Options.OutputDir = Directory.GetCurrentDirectory();

            for (var i = 0; i < Options.IncludeDirs.Count; i++)
            {
                Options.IncludeDirs[i] = Path.GetFullPath(Options.IncludeDirs[i]);
            }
        }

        private void OnHeaderParsed(string file, ParserResult result)
        {
            switch (result.Kind)
            {
                case ParserResultKind.Success:
                    Console.WriteLine("  Parsed '{0}'", file);
                    break;
                case ParserResultKind.Error:
                    Console.WriteLine("  Error parsing '{0}'", file);
                    break;
                case ParserResultKind.FileNotFound:
                    Console.WriteLine("  File '{0}' was not found", file);
                    break;
            }

            foreach (var diag in result.Diagnostics)
                Console.WriteLine("    {0}", diag);
        }

        public void ParseCode()
        {
            Console.WriteLine("Parsing code...");

            var parser = new Parser(Options);
            parser.HeaderParsed += OnHeaderParsed;

            parser.ParseHeaders(Options.Headers);
            Library = parser.Library;
        }

        public void ProcessCode()
        {
            if (Transform != null)
                Transform.Preprocess(Library);

            var passes = new PassBuilder(Library);
            passes.SortDeclarations();
            passes.ResolveIncompleteDecls(TypeDatabase);
            passes.CheckFlagEnums();

            if (Transform != null)
                Transform.SetupPasses(passes);

            passes.CleanInvalidDeclNames();

            var transformer = new Transform() { Options = Options, Passes = passes };
            transformer.TransformLibrary(Library);

            if (Transform != null)
                Transform.Postprocess(Library);
        }

        public void GenerateCode()
        {
            if (Library.TranslationUnits.Count <= 0)
                return;

            Console.WriteLine("Generating wrapper code...");

            Generator generator = null;

            switch (Options.GeneratorKind)
            {
                case LanguageGeneratorKind.CSharp:
                    generator = new CSharpGenerator(this);
                    break;
                case LanguageGeneratorKind.CPlusPlusCLI:
                default:
                    generator = new CLIGenerator(this);
                    break;
            }

            if (!Directory.Exists(Options.OutputDir))
                Directory.CreateDirectory(Options.OutputDir);

            // Process everything in the global namespace for now.
            foreach (var unit in Library.TranslationUnits)
            {
                if (unit.ExplicityIgnored || !unit.HasDeclarations)
                    continue;

                if (unit.IsSystemHeader)
                    continue;

                // Generate the target code.
                generator.Generate(unit);
            }
        }

        public static void Run(ILibrary library)
        {
            var options = new DriverOptions();

            var driver = new Driver(options, library);
            driver.Setup();
            driver.ParseCode();
            driver.ProcessCode();
            driver.GenerateCode();
        }
    }

    public class DriverOptions
    {
        public DriverOptions()
        {
            Defines = new List<string>();
            IncludeDirs = new List<string>();
            Headers = new List<string>();
            Assembly = string.Empty;
            GeneratorKind = LanguageGeneratorKind.CSharp;
        }

        public bool Verbose = false;
        public bool ShowHelpText = false;
        public bool OutputDebug = false;
        public string OutputNamespace;
        public string OutputDir;
        public string LibraryName;
        public List<string> Defines;
        public List<string> IncludeDirs;
        public List<string> Headers;
        public string Template;
        public string Assembly;
        public int ToolsetToUse;
        public string IncludePrefix;
        public string WrapperSuffix;
        public LanguageGeneratorKind GeneratorKind;
    }
}